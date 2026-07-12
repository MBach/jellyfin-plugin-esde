using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EsDe;

/// <summary>
/// Injects the ES-DE client script into Jellyfin's index.html on startup,
/// and removes it on shutdown.
/// </summary>
public class ScriptInjector : IHostedService
{
    private const string ScriptTag = "<!-- ES-DE Plugin --><script src=\"/EsDe/Script\" defer></script><!-- /ES-DE Plugin -->";
    private readonly ILogger<ScriptInjector> _logger;
    private readonly IServerApplicationHost _appHost;
    private string? _indexPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptInjector"/> class.
    /// </summary>
    public ScriptInjector(ILogger<ScriptInjector> logger, IServerApplicationHost appHost)
    {
        _logger = logger;
        _appHost = appHost;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _indexPath = FindIndexHtml();
        if (_indexPath is null)
        {
            _logger.LogWarning("[ES-DE] Could not locate index.html — client script will not be injected");
            return Task.CompletedTask;
        }

        try
        {
            var html = File.ReadAllText(_indexPath);

            // Don't inject twice
            if (html.Contains("ES-DE Plugin"))
            {
                _logger.LogInformation("[ES-DE] Script already injected in {Path}", _indexPath);
                return Task.CompletedTask;
            }

            // Inject before </body>
            html = html.Replace("</body>", ScriptTag + "\n</body>");
            File.WriteAllText(_indexPath, html);
            _logger.LogInformation("[ES-DE] Script injected into {Path}", _indexPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ES-DE] Failed to inject script into {Path}", _indexPath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_indexPath is null) return Task.CompletedTask;

        try
        {
            var html = File.ReadAllText(_indexPath);
            if (html.Contains("ES-DE Plugin"))
            {
                html = html.Replace(ScriptTag + "\n", string.Empty);
                File.WriteAllText(_indexPath, html);
                _logger.LogInformation("[ES-DE] Script removed from {Path}", _indexPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ES-DE] Failed to clean up script from {Path}", _indexPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Locates the jellyfin-web index.html file.
    /// </summary>
    private string? FindIndexHtml()
    {
        // Common locations across platforms
        string[] candidates = new[]
        {
            // Windows (installed)
            Path.Combine(AppContext.BaseDirectory, "jellyfin-web", "index.html"),
            // Windows (tray install)
            Path.Combine(AppContext.BaseDirectory, "system", "jellyfin-web", "index.html"),
            // Linux (package install)
            "/usr/share/jellyfin/web/index.html",
            // Linux (manual)
            "/usr/lib/jellyfin/web/index.html",
            // Portable
            Path.Combine(AppContext.BaseDirectory, "web", "index.html"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
