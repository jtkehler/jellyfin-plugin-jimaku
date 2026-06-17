using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Jimaku.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Jimaku;

/// <summary>
/// The Jimaku subtitle plugin.
/// </summary>
public class JimakuPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JimakuPlugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public JimakuPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name
        => "Jimaku";

    /// <inheritdoc />
    public override Guid Id
        => Guid.Parse("859cd24d-e976-423d-9f24-38a9f037cc0b");

    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    public static JimakuPlugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "jimaku",
                EmbeddedResourcePath = GetType().Namespace + ".Web.jimaku.html",
            },
            new PluginPageInfo
            {
                Name = "jimakujs",
                EmbeddedResourcePath = GetType().Namespace + ".Web.jimaku.js"
            }
        };
    }
}
