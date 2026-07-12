using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EsDe.Api;

/// <summary>
/// API controller for launching and managing ES-DE.
/// </summary>
[ApiController]
[Route("EsDe")]
public class EsDeController : ControllerBase
{
    private readonly ILogger<EsDeController> _logger;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private const string LauncherBaseUrl = "http://127.0.0.1:9999";
    private static readonly SemaphoreSlim _launchLock = new(1, 1);
    private static bool _launchInFlight;

    // Well-known paths where ES-DE might be installed
    private static readonly string[] LinuxPaths =
    [
        "/usr/bin/es-de",
        "/usr/local/bin/es-de",
        "/usr/bin/emulationstation",
        "/opt/es-de/es-de"
    ];

    private static readonly string[] WindowsPaths =
    [
        @"C:\Program Files\ES-DE\ES-DE.exe",
        @"C:\Program Files (x86)\ES-DE\ES-DE.exe"
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="EsDeController"/> class.
    /// </summary>
    public EsDeController(ILogger<EsDeController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the current status of ES-DE.
    /// </summary>
    [HttpGet("Status")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult Status()
    {
        var binaryPath = DetectBinary();
        var running = IsEsDeRunning();

        return Ok(new
        {
            detected = binaryPath is not null,
            binaryPath,
            running
        });
    }

    /// <summary>
    /// Launches ES-DE via the local launcher service.
    /// </summary>
    [HttpPost("Launch")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Launch()
    {
        var binaryPath = DetectBinary();
        if (binaryPath is null)
        {
            return NotFound(new { message = "ES-DE binary not found on this system." });
        }

        if (IsEsDeRunning())
        {
            return Conflict(new { message = "ES-DE is already running." });
        }

        if (!await _launchLock.WaitAsync(0))
        {
            return Conflict(new { message = "ES-DE launch already in progress." });
        }

        try
        {
            if (_launchInFlight || IsEsDeRunning())
            {
                return Conflict(new { message = "ES-DE is already running." });
            }

            _launchInFlight = true;
            try
            {
                var response = await _httpClient.GetAsync($"{LauncherBaseUrl}/launch-games");
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("[ES-DE] Launcher response: {Body}", body);
                return Ok(new { message = "ES-DE launched via launcher." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ES-DE] Failed to call launcher");
                return StatusCode(500, new { message = $"Launcher unreachable: {ex.Message}" });
            }
            finally
            {
                // Give ES-DE a moment to actually appear in the process list before releasing,
                // so a request arriving right after this one still sees it as running.
                _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
                {
                    _launchInFlight = false;
                });
            }
        }
        finally
        {
            _launchLock.Release();
        }
    }

    /// <summary>
    /// Stops ES-DE via the local launcher service.
    /// </summary>
    [HttpPost("Stop")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Stop()
    {
        if (!IsEsDeRunning())
        {
            return NotFound(new { message = "ES-DE is not running." });
        }

        try
        {
            var response = await _httpClient.PostAsync($"{LauncherBaseUrl}/stop-esde", null);
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("[ES-DE] Stop response: {Body}", body);
            return Ok(new { message = "ES-DE stopped." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ES-DE] Failed to stop via launcher");
            return StatusCode(500, new { message = $"Launcher unreachable: {ex.Message}" });
        }
    }

    /// <summary>
    /// Serves the client-side JavaScript for the ES-DE plugin.
    /// </summary>
    [HttpGet("Script")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult Script()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Jellyfin.Plugin.EsDe.Web.esde.js";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return NotFound();
        }

        using var reader = new StreamReader(stream);
        return Content(reader.ReadToEnd(), "application/javascript");
    }

    /// <summary>
    /// Checks if an ES-DE process is currently running on the system.
    /// </summary>
    private static bool IsEsDeRunning()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Process.GetProcessesByName("ES-DE").Length > 0;
            }

            var psi = new ProcessStartInfo("pgrep", "-x es-de")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(2000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Auto-detects the ES-DE binary on the system.
    /// </summary>
    private static string? DetectBinary()
    {
        var paths = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsPaths
            : LinuxPaths;

        var found = paths.FirstOrDefault(System.IO.File.Exists);
        if (found is not null) return found;

        try
        {
            var whichCmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
            var psi = new ProcessStartInfo(whichCmd, "es-de")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            var output = proc.StandardOutput.ReadLine()?.Trim();
            proc.WaitForExit(2000);

            return proc.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
