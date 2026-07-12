using System;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.EsDe;

/// <summary>
/// ES-DE Launcher plugin — launches ES-DE directly from the Jellyfin home screen.
/// </summary>
public class Plugin : BasePlugin<BasePluginConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "ES-DE Launcher";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("195b800f-1fe1-4eff-9fcd-ad25f9f61c0c");

    /// <inheritdoc />
    public override string Description => "Launch ES-DE (EmulationStation Desktop Edition) directly from Jellyfin.";

    /// <inheritdoc />
    public Stream? GetJavascriptStream()
    {
        return GetType().Assembly.GetManifestResourceStream(
            "Jellyfin.Plugin.EsDe.Web.esde.js");
    }
}