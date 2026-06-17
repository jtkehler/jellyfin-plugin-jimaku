using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Jimaku.Models;

/// <summary>
/// The API error response.
/// </summary>
public class ApiError
{
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }
}
