using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Jimaku.Configuration;

/// <summary>
/// The plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the API key is invalid.
    /// </summary>
    public bool ApiKeyInvalid { get; set; }
}
