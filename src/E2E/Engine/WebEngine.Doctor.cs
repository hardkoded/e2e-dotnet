// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Engine;

/// <summary>
/// The doctor half of the engine (upstream <c>surface.ts</c> <c>doctor</c>, <c>install.ts</c> <c>browserCheck</c>, and
/// <c>init-scripts.ts</c> <c>doctor</c>): the checks <c>e2e doctor</c> asks for, without starting a browser.
/// </summary>
public sealed partial class WebEngine : IDoctorEngine
{
    private const string BrowsersPathVariable = "PLAYWRIGHT_BROWSERS_PATH";

    /// <summary>
    /// The checks of <c>e2e doctor</c>: the init scripts a start reads, and the browser, which only a local launch needs on this machine.
    /// </summary>
    public async Task<IReadOnlyList<EngineDoctorCheck>> DoctorAsync(EngineDoctorInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);
        var scripts = await InitScriptsCheckAsync(info.ProjectRoot, cancellationToken).ConfigureAwait(false);
        if (_leases is not null)
        {
            return [.. scripts, new EngineDoctorCheck("browser", EngineDoctorStatus.Skip, $"browsers come from the \"{_leases.Name}\" provider")];
        }

        if (_options.Connect is not null)
        {
            return [.. scripts, new EngineDoctorCheck("browser", EngineDoctorStatus.Skip, "the browser is attached over CDP")];
        }

        return [.. scripts, .. BrowserCheck(_options.BrowserName, info.Env, info.Headed)];
    }

    /// <summary>The <c>init-scripts</c> check of <c>e2e doctor</c>: none without scripts, else whether every file reads (upstream's <c>ConfiguredInitScripts.doctor</c>).</summary>
    private async Task<IReadOnlyList<EngineDoctorCheck>> InitScriptsCheckAsync(string projectRoot, CancellationToken cancellationToken)
    {
        var count = _options.InitScripts?.Count ?? 0;
        if (count == 0)
        {
            return [];
        }

        try
        {
            await ReadInitScriptsAsync(projectRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (EngineException ex)
        {
            return [new EngineDoctorCheck("init-scripts", EngineDoctorStatus.Fail, ex.Message)];
        }

        return [new EngineDoctorCheck("init-scripts", EngineDoctorStatus.Pass, count.ToString(System.Globalization.CultureInfo.InvariantCulture) + (count == 1 ? " script reads" : " scripts read"))];
    }

    /// <summary>
    /// The <c>browser</c> check of <c>e2e doctor</c>: whether the build a run launches is on disk, a <c>warn</c> when the first run will
    /// download it, or none when <paramref name="env"/> points at another browser cache than this process loaded (upstream's <c>browserCheck</c>).
    /// <paramref name="isInstalled"/> is a test seam.
    /// </summary>
    internal static IReadOnlyList<EngineDoctorCheck> BrowserCheck(
        BrowserName name,
        IReadOnlyDictionary<string, string?> env,
        bool headed,
        Func<BrowserName, bool?>? isInstalled = null)
    {
        var installed = (isInstalled ?? (browser => IsBrowserInstalled(browser, env, headed)))(name);
        var browserName = BrowserConnection.NameOf(name);
        return installed switch
        {
            null => [],
            true => [new EngineDoctorCheck("browser", EngineDoctorStatus.Pass, browserName + " is installed")],
            false => [new EngineDoctorCheck("browser", EngineDoctorStatus.Warn, $"{browserName} is not installed; the first run downloads it, or run \"e2e install {browserName}\" now")],
        };
    }

    /// <summary>
    /// Whether the build the run launches (the headless shell for headless Chromium, else the full build) finished installing in the
    /// browser cache, or null when <paramref name="env"/> points at another cache than this process loaded, or the cache or the
    /// revision cannot be told (upstream's <c>isBrowserInstalled</c>). The port reads the revision from the driver's <c>browsers.json</c>
    /// instead of asking a driver, which would start a process.
    /// </summary>
    private static bool? IsBrowserInstalled(BrowserName name, IReadOnlyDictionary<string, string?> env, bool headed)
    {
        var declared = env.GetValueOrDefault(BrowsersPathVariable);
        if (!string.Equals(declared, Environment.GetEnvironmentVariable(BrowsersPathVariable), StringComparison.Ordinal))
        {
            return null;
        }

        var cache = BrowsersCache(declared);
        var product = name == BrowserName.Chromium && !headed ? "chromium-headless-shell" : BrowserConnection.NameOf(name);
        if (cache is null || BrowserRevision(product) is not { } revision)
        {
            return null;
        }

        return File.Exists(Path.Combine(cache, product.Replace('-', '_') + "-" + revision, "INSTALLATION_COMPLETE"));
    }

    /// <summary>Playwright's browser cache: the declared path, else the operating system's default. Null for <c>0</c>, which places it inside the driver.</summary>
    private static string? BrowsersCache(string? declared)
    {
        if (!string.IsNullOrEmpty(declared))
        {
            return declared == "0" ? null : Path.GetFullPath(declared);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            var local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            return string.IsNullOrEmpty(local) ? null : Path.Combine(local, "ms-playwright");
        }

        if (OperatingSystem.IsMacOS())
        {
            return string.IsNullOrEmpty(home) ? null : Path.Combine(home, "Library", "Caches", "ms-playwright");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        var root = !string.IsNullOrEmpty(xdg) ? xdg : string.IsNullOrEmpty(home) ? null : Path.Combine(home, ".cache");
        return root is null ? null : Path.Combine(root, "ms-playwright");
    }

    /// <summary>The revision of <paramref name="product"/> in the driver's <c>browsers.json</c>, or null when the file does not say.</summary>
    private static string? BrowserRevision(string product)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ".playwright", "package", "browsers.json")));
            return document.RootElement.GetProperty("browsers").EnumerateArray()
                .Where(browser => browser.GetProperty("name").GetString() == product)
                .Select(browser => browser.GetProperty("revision").GetString())
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
