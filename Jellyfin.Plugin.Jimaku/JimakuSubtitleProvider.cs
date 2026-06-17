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
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
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
    private const string JimakuLanguage = "jpn";
    private readonly ILogger<JimakuSubtitleProvider> _logger;
    private readonly ConcurrentBag<string> _badSubtitleUrls = new ();
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JimakuSubtitleProvider"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{JimakuSubtitleProvider}"/> interface.</param>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> for creating Http Clients.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public JimakuSubtitleProvider(
        ILogger<JimakuSubtitleProvider> logger,
        IHttpClientFactory httpClientFactory,
        ILibraryManager libraryManager)
    {
        Instance = this;
        _logger = logger;
        _libraryManager = libraryManager;
        JimakuRequestHelper.Instance = new JimakuRequestHelper(httpClientFactory);
        _logger.LogInformation("JimakuSubtitleProvider constructed");
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
        try
        {
            return await SearchInternal(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unhandled exception in Jimaku Search");
            return Enumerable.Empty<RemoteSubtitleInfo>();
        }
    }

    private async Task<IEnumerable<RemoteSubtitleInfo>> SearchInternal(SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Search: type={ContentType} path={MediaPath} lang={Language} automated={IsAutomated} perfectMatch={IsPerfectMatch}",
            request.ContentType.ToString(),
            request.MediaPath ?? "(null)",
            request.Language ?? "(null)",
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

        // Resolve AniList ID from the request or, for episodes, the parent series.
        int? anilistId = GetAniListId(request);

        // Try to get TMDB ID (available for movies directly; may be absent for episodes).
        var tmdbIdRaw = request.GetProviderId(MetadataProvider.Tmdb);
        long.TryParse(tmdbIdRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var tmdbIdNumeric);

        _logger.LogInformation(
            "Provider IDs: AniList={AniList} Tmdb={Tmdb} keys=[{Keys}]",
            anilistId?.ToString(CultureInfo.InvariantCulture) ?? "(null)",
            tmdbIdRaw ?? "(null)",
            string.Join(", ", request.ProviderIds.Keys));

        var entries = new List<Entry>();

        if (anilistId.HasValue && anilistId.Value > 0)
        {
            _logger.LogInformation("Searching by AniList ID: {AniListId}", anilistId);

            var response = await JimakuApi.SearchEntriesAsync(null, null, anilistId, null, null, null, cancellationToken).ConfigureAwait(false);
            if (response.Ok && response.Data is not null)
            {
                entries.AddRange(response.Data);
            }
            else if (!response.Ok)
            {
                _logger.LogWarning("AniList search failed: {Code} {Body}", response.Code, response.Body);
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
            else if (!response.Ok)
            {
                _logger.LogWarning("TMDB search failed (anime=false): {Code} {Body}", response.Code, response.Body);
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
                else if (!response.Ok)
                {
                    _logger.LogWarning("TMDB search failed (anime=true): {Code} {Body}", response.Code, response.Body);
                }
            }
        }

        if (entries.Count == 0)
        {
            var query = BuildSearchQuery(request);

            _logger.LogInformation("Searching by query: {Query} (anime=false)", query);

            var response = await JimakuApi.SearchEntriesAsync(query, null, null, false, null, null, cancellationToken).ConfigureAwait(false);
            if (response.Ok && response.Data is not null)
            {
                entries.AddRange(response.Data);
            }
            else if (!response.Ok)
            {
                _logger.LogWarning("Query search failed (anime=false): {Code} {Body}", response.Code, response.Body);
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
                else if (!response.Ok)
                {
                    _logger.LogWarning("Query search failed (anime=true): {Code} {Body}", response.Code, response.Body);
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
                _logger.LogWarning("Failed to get files for entry {EntryId}: {Code} {Body}", entry.Id, filesResponse.Code, filesResponse.Body);
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
                    ThreeLetterISOLanguageName = JimakuLanguage,
                    Id = BuildSubtitleId(format, JimakuLanguage, file, entry.Id),
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

    private int? GetAniListId(SubtitleSearchRequest request)
    {
        var direct = TryGetAniListId(request.ProviderIds);
        if (direct.HasValue)
        {
            _logger.LogInformation("Found AniList ID on subtitle request: {AniListId}", direct.Value);
            return direct;
        }

        if (request.ContentType == VideoContentType.Episode
            && !string.IsNullOrEmpty(request.MediaPath)
            && _libraryManager.FindByPath(request.MediaPath, false) is Episode episode)
        {
            var series = episode.Series;
            if (series is not null)
            {
                var seriesAniList = TryGetAniListId(series.ProviderIds);
                if (seriesAniList.HasValue)
                {
                    _logger.LogInformation(
                        "Found AniList ID on parent series {SeriesName}: {AniListId}",
                        series.Name,
                        seriesAniList.Value);

                    return seriesAniList;
                }

                _logger.LogInformation(
                    "Parent series {SeriesName} did not have an AniList provider ID",
                    series.Name);
            }
            else
            {
                _logger.LogInformation("Episode was found by path, but parent series was null");
            }
        }

        _logger.LogInformation("No AniList ID found for subtitle request");
        return null;
    }

    private static int? TryGetAniListId(IEnumerable<KeyValuePair<string, string>> providerIds)
    {
        foreach (var kvp in providerIds)
        {
            if (string.Equals(kvp.Key, "AniList", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(kvp.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var id))
            {
                return id;
            }
        }

        return null;
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

    private static string BuildSearchQuery(SubtitleSearchRequest request)
    {
        if (!string.IsNullOrEmpty(request.SeriesName))
        {
            return request.SeriesName;
        }

        var filename = Path.GetFileNameWithoutExtension(request.MediaPath);

        // Strip common release-group and codec tags from bracketed sections
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            filename,
            @"\[(?:[\w-]+(?:\s+[\w-]+)*)\]\s*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromMilliseconds(200));

        return cleaned.Trim();
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
