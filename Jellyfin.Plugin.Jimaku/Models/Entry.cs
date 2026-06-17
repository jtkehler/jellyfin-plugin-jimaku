using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jimaku.Models;

/// <summary>
/// An entry that contains subtitles.
/// </summary>
public class Entry
{
    /// <summary>
    /// Gets or sets the ID of the entry.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the romaji name of the entry.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the English name of the entry.
    /// </summary>
    [JsonPropertyName("english_name")]
    public string? EnglishName { get; set; }

    /// <summary>
    /// Gets or sets the Japanese name of the entry.
    /// </summary>
    [JsonPropertyName("japanese_name")]
    public string? JapaneseName { get; set; }

    /// <summary>
    /// Gets or sets the AniList ID of this entry.
    /// </summary>
    [JsonPropertyName("anilist_id")]
    public int? AnilistId { get; set; }

    /// <summary>
    /// Gets or sets the TMDB ID of this entry.
    /// </summary>
    [JsonPropertyName("tmdb_id")]
    public string? TmdbId { get; set; }

    /// <summary>
    /// Gets or sets the flags on this entry.
    /// </summary>
    [JsonPropertyName("flags")]
    public EntryFlags Flags { get; set; } = new ();

    /// <summary>
    /// Gets or sets the date of the newest uploaded file.
    /// </summary>
    [JsonPropertyName("last_modified")]
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Gets or sets extra notes for the entry.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the account ID that created this entry.
    /// </summary>
    [JsonPropertyName("creator_id")]
    public long? CreatorId { get; set; }
}
