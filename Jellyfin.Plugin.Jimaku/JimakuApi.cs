using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jimaku.Models;

namespace Jellyfin.Plugin.Jimaku;

/// <summary>
/// The Jimaku API client.
/// </summary>
public static class JimakuApi
{
    /// <summary>
    /// Search for entries.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="tmdbId">The TMDB ID to filter by.</param>
    /// <param name="anilistId">The AniList ID to filter by.</param>
    /// <param name="anime">Whether to return only anime entries.</param>
    /// <param name="after">Return entries after this UNIX timestamp.</param>
    /// <param name="before">Return entries before this UNIX timestamp.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of matching entries.</returns>
    public static async Task<ApiResponse<IReadOnlyList<Entry>>> SearchEntriesAsync(
        string? query,
        string? tmdbId,
        int? anilistId,
        bool? anime,
        long? after,
        long? before,
        CancellationToken cancellationToken)
    {
        var options = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(query))
        {
            options["query"] = query;
        }

        if (!string.IsNullOrEmpty(tmdbId))
        {
            options["tmdb_id"] = tmdbId;
        }

        if (anilistId.HasValue)
        {
            options["anilist_id"] = anilistId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (anime.HasValue)
        {
            options["anime"] = anime.Value ? "true" : "false";
        }

        if (after.HasValue)
        {
            options["after"] = after.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (before.HasValue)
        {
            options["before"] = before.Value.ToString(CultureInfo.InvariantCulture);
        }

        var url = RequestHandler.AddQueryString("/entries/search", options);
        var response = await RequestHandler.SendRequestAsync(url, HttpMethod.Get, null, null, 1, cancellationToken).ConfigureAwait(false);

        return new ApiResponse<IReadOnlyList<Entry>>(response, $"url: {url}");
    }

    /// <summary>
    /// Get the files for an entry.
    /// </summary>
    /// <param name="entryId">The entry ID.</param>
    /// <param name="episode">The episode number to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of files.</returns>
    public static async Task<ApiResponse<IReadOnlyList<FileEntry>>> GetEntryFilesAsync(
        long entryId,
        int? episode,
        CancellationToken cancellationToken)
    {
        var url = $"/entries/{entryId}/files";

        if (episode.HasValue)
        {
            url += $"?episode={episode.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        var response = await RequestHandler.SendRequestAsync(url, HttpMethod.Get, null, null, 1, cancellationToken).ConfigureAwait(false);

        return new ApiResponse<IReadOnlyList<FileEntry>>(response, $"entryId: {entryId}");
    }

    /// <summary>
    /// Validates an API key by making a test request.
    /// </summary>
    /// <param name="apiKey">The API key to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An ApiResponse indicating success or failure.</returns>
    public static async Task<ApiResponse<object?>> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        var response = await RequestHandler.SendRequestAsync(
            "/entries/search?query=test",
            HttpMethod.Get,
            null,
            null,
            1,
            cancellationToken,
            false,
            apiKey).ConfigureAwait(false);

        return new ApiResponse<object?>(response, "validate key");
    }
}
