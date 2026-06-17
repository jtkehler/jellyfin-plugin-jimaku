using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jimaku.Configuration;
using Jellyfin.Plugin.Jimaku.Models;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jimaku;

/// <summary>
/// The Jimaku subtitle provider.
/// </summary>
public class JimakuSubtitleProvider : ISubtitleProvider
{
    private readonly ILogger<JimakuSubtitleProvider> _logger;
    private readonly ConcurrentBag<string> _badSubtitleUrls = new ();

    /// <summary>
    /// Initializes a new instance of the <see cref="JimakuSubtitleProvider"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{JimakuSubtitleProvider}"/> interface.</param>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> for creating Http Clients.</param>
    public JimakuSubtitleProvider(ILogger<JimakuSubtitleProvider> logger, IHttpClientFactory httpClientFactory)
    {
        Instance = this;
        _logger = logger;
        JimakuRequestHelper.Instance = new JimakuRequestHelper(httpClientFactory);
    }

    /// <summary>
    /// Gets the provider instance.
    /// </summary>
    public static JimakuSubtitleProvider? Instance { get; private set; }

    /// <inheritdoc />
    public string Name => "Jimaku";

    /// <inheritdoc />
    public IEnumerable<VideoContentType> SupportedMediaTypes
        => new[] { VideoContentType.Episode, VideoContentType.Movie };

    /// <inheritdoc />
    public Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        => GetSubtitlesInternal(id, cancellationToken);

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Search request: type={ContentType}, path={MediaPath}, language={Language}, automated={IsAutomated}, perfectMatch={IsPerfectMatch}",
            request.ContentType,
            request.MediaPath,
            request.Language,
            request.IsAutomated,
            request.IsPerfectMatch);

        var config = JimakuPlugin.Instance?.Configuration;
        if (config is null || string.IsNullOrEmpty(config.ApiKey) || config.ApiKeyInvalid)
        {
            _logger.LogInformation("API key not configured or invalid, returning empty results");
            return Enumerable.Empty<RemoteSubtitleInfo>();
        }

        if (request.ContentType == VideoContentType.Episode && (!request.IndexNumber.HasValue || !request.ParentIndexNumber.HasValue || string.IsNullOrEmpty(request.SeriesName)))
        {
            _logger.LogInformation("Episode information missing");
            return Enumerable.Empty<RemoteSubtitleInfo>();
        }

        if (string.IsNullOrEmpty(request.MediaPath))
        {
            _logger.LogInformation("Path missing");
            return Enumerable.Empty<RemoteSubtitleInfo>();
        }

        // Try to get AniList ID (available when AniList metadata plugin is installed)
        request.ProviderIds.TryGetValue("AniList", out var anilistIdStr);
        int.TryParse(anilistIdStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var anilistId);

        // Try to get TMDB ID
        var tmdbIdRaw = request.GetProviderId(MetadataProvider.Tmdb);
        long.TryParse(tmdbIdRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var tmdbIdNumeric);

        var entries = new List<Entry>();

        if (anilistId > 0)
        {
            _logger.LogInformation("Searching by AniList ID: {AniListId}", anilistId);

            var response = await JimakuApi.SearchEntriesAsync(null, null, anilistId, null, null, null, cancellationToken).ConfigureAwait(false);
            if (response.Ok && response.Data is not null)
            {
                entries.AddRange(response.Data);
            }
        }

        if (entries.Count == 0 && tmdbIdNumeric > 0)
        {
            var isEpisode = request.ContentType == VideoContentType.Episode;
            var tmdbId = isEpisode ? $"tv:{tmdbIdNumeric}" : $"movie:{tmdbIdNumeric}";

            _logger.LogInformation("Searching by TMDB ID: {TmdbId} (anime=false)", tmdbId);

            var response = await JimakuApi.SearchEntriesAsync(null, tmdbId, null, false, null, null, cancellationToken).ConfigureAwait(false);
            if (response.Ok && response.Data is not null)
            {
                entries.AddRange(response.Data);
            }

            // Fallback: try anime=true for TMDB IDs that might be anime entries
            if (entries.Count == 0)
            {
                _logger.LogInformation("No results with anime=false, trying anime=true fallback");

                response = await JimakuApi.SearchEntriesAsync(null, tmdbId, null, true, null, null, cancellationToken).ConfigureAwait(false);
                if (response.Ok && response.Data is not null)
                {
                    entries.AddRange(response.Data);
                }
            }
        }

        if (entries.Count == 0)
        {
            var query = Path.GetFileNameWithoutExtension(request.MediaPath);

            _logger.LogInformation("Searching by query: {Query} (anime=false)", query);

            var response = await JimakuApi.SearchEntriesAsync(query, null, null, false, null, null, cancellationToken).ConfigureAwait(false);
            if (response.Ok && response.Data is not null)
            {
                entries.AddRange(response.Data);
            }

            // Fallback: try anime=true
            if (entries.Count == 0)
            {
                _logger.LogInformation("No results with anime=false, trying anime=true fallback");

                response = await JimakuApi.SearchEntriesAsync(query, null, null, true, null, null, cancellationToken).ConfigureAwait(false);
                if (response.Ok && response.Data is not null)
                {
                    entries.AddRange(response.Data);
                }
            }
        }

        if (entries.Count == 0)
        {
            _logger.LogInformation("No entries found for {MediaPath}", request.MediaPath);
            return Enumerable.Empty<RemoteSubtitleInfo>();
        }

        // Fetch files for each entry and build subtitle info list
        var results = new List<RemoteSubtitleInfo>();
        foreach (var entry in entries)
        {
            var filesResponse = await JimakuApi.GetEntryFilesAsync(
                entry.Id,
                request.ContentType == VideoContentType.Episode ? request.IndexNumber : null,
                cancellationToken).ConfigureAwait(false);

            if (!filesResponse.Ok || filesResponse.Data is null)
            {
                _logger.LogInformation("Failed to get files for entry {EntryId}: {Code}", entry.Id, filesResponse.Code);
                continue;
            }

            foreach (var file in filesResponse.Data)
            {
                if (string.IsNullOrEmpty(file.Url) || string.IsNullOrEmpty(file.Name))
                {
                    continue;
                }

                var format = Path.GetExtension(file.Name).TrimStart('.').ToLowerInvariant();
                if (string.IsNullOrEmpty(format))
                {
                    format = "srt";
                }

                results.Add(new RemoteSubtitleInfo
                {
                    Author = entry.EnglishName ?? entry.Name,
                    Comment = entry.Notes,
                    Format = format,
                    ProviderName = Name,
                    ThreeLetterISOLanguageName = request.Language,
                    Id = BuildSubtitleId(format, request.Language, file, entry.Id),
                    Name = file.Name,
                    DateCreated = file.LastModified,
                    IsHashMatch = false,
                    HearingImpaired = false,
                    MachineTranslated = null,
                    AiTranslated = null,
                    FrameRate = null,
                    Forced = false
                });
            }
        }

        _logger.LogInformation("Returning {Count} subtitle results", results.Count);
        return results;
    }

    private static string BuildSubtitleId(string format, string language, FileEntry file, long entryId)
    {
        var payload = new SubtitleIdPayload
        {
            Format = format,
            Language = language,
            Url = file.Url,
            Name = file.Name,
            EntryId = entryId
        };

        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private async Task<SubtitleResponse> GetSubtitlesInternal(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Missing param", nameof(id));
        }

        SubtitleIdPayload payload;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(id));
            payload = JsonSerializer.Deserialize<SubtitleIdPayload>(json) ?? throw new FormatException("Empty payload");
        }
        catch (Exception ex) when (ex is not ArgumentException and not FormatException)
        {
            throw new FormatException($"Invalid subtitle id format: {id}", ex);
        }

        if (string.IsNullOrEmpty(payload.Url))
        {
            throw new FormatException($"Invalid subtitle id: missing URL");
        }

        if (_badSubtitleUrls.Contains(payload.Url))
        {
            throw new HttpRequestException($"Subtitle URL {payload.Url} previously returned empty content");
        }

        var isFullUrl = payload.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || payload.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        var response = await RequestHandler.SendRequestAsync(
            payload.Url,
            HttpMethod.Get,
            null,
            null,
            1,
            cancellationToken,
            isFullUrl).ConfigureAwait(false);

        if (response.Code != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Body))
        {
            if (response.Code == HttpStatusCode.OK && string.IsNullOrWhiteSpace(response.Body))
            {
                if (!_badSubtitleUrls.Contains(payload.Url))
                {
                    _badSubtitleUrls.Add(payload.Url);
                }
            }

            var msg = string.Format(
                CultureInfo.InvariantCulture,
                "Subtitle {0} could not be downloaded: {1}",
                payload.Name,
                response.Code);

            throw new HttpRequestException(msg);
        }

        return new SubtitleResponse
        {
            Format = payload.Format,
            Language = payload.Language,
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(response.Body)),
            IsForced = false,
            IsHearingImpaired = false
        };
    }

    internal void ConfigurationChanged(PluginConfiguration configuration)
    {
        // Configuration is read directly from JimakuPlugin.Instance.Configuration on each request.
    }

    private sealed class SubtitleIdPayload
    {
        public string Format { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public long EntryId { get; set; }
    }
}
