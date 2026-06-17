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

    /// <summary>
    /// Gets or sets comma-separated keywords for prioritizing subtitle results.
    /// Subtitle files whose names contain any of these keywords will be sorted first.
    /// </summary>
    public string PreferredKeywords { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets comma-separated terms for blacklisting subtitle results.
    /// Subtitle files whose names contain any of these terms will be hidden from results.
    /// </summary>
    public string BlacklistedTerms { get; set; } = string.Empty;
}
