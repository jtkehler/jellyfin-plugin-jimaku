using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jimaku.Models;

/// <summary>
/// Flags associated with an entry.
/// </summary>
public class EntryFlags
{
    /// <summary>
    /// Gets or sets a value indicating whether the entry is for adult audiences.
    /// </summary>
    [JsonPropertyName("adult")]
    public bool Adult { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry is for an anime.
    /// </summary>
    [JsonPropertyName("anime")]
    public bool Anime { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry comes from an external source.
    /// </summary>
    [JsonPropertyName("external")]
    public bool External { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry is a movie.
    /// </summary>
    [JsonPropertyName("movie")]
    public bool Movie { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry is unverified.
    /// </summary>
    [JsonPropertyName("unverified")]
    public bool Unverified { get; set; }
}
