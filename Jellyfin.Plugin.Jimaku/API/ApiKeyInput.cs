using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Plugin.Jimaku.API;

/// <summary>
/// The API key input model.
/// </summary>
public class ApiKeyInput
{
    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = null!;
}
