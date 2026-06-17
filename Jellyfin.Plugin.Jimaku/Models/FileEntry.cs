using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jimaku.Models;

/// <summary>
/// Represents a file entry (e.g. a subtitle file).
/// </summary>
public class FileEntry
{
    /// <summary>
    /// Gets or sets the file's download URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file's name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file's size in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the date the file was last modified.
    /// </summary>
    [JsonPropertyName("last_modified")]
    public DateTime LastModified { get; set; }
}
