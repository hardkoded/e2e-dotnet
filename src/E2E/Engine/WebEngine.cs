// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using E2E.Internal;
using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>A page size in CSS pixels.</summary>
public sealed record WebViewport(int Width, int Height)
{
    /// <summary>The size every attempt's page starts with unless the options say otherwise: 1280 by 720.</summary>
    public static WebViewport Default { get; } = new(1280, 720);
}

/// <summary>
/// How the engine's own screencast of the page records an attempt that records video: the frame size and the JPEG quality
/// of the frames it encodes. Which attempts record is the config's <c>video</c>, not an engine option. Upstream's
/// <c>WebScreencastOptions</c>.
/// </summary>
public sealed record WebScreencastOptions
{
    /// <summary>The frame size in whole pixels. Defaults to the page's viewport size when the segment starts.</summary>
    public WebViewport? Size { get; init; }

    /// <summary>The JPEG quality of the frames, 0 through 100. Defaults to Playwright's.</summary>
    public int? Quality { get; init; }
}

/// <summary>HTTP basic authentication the browser answers a <c>401</c> challenge with.</summary>
public sealed class WebBasicAuth
{
    public WebBasicAuth(string username, string password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        Username = username;
        Password = password;
    }

    public WebBasicAuth(string username, Secret password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        Username = username;
        Password = password;
    }

    /// <summary>The user name. RFC 7617 does not allow <c>:</c> in one.</summary>
    public string Username { get; }

    /// <summary>The password: a <see cref="string"/>, or a <see cref="Secret"/> the attempt resolves.</summary>
    internal object Password { get; }
}

/// <summary>
/// A script every document runs before the page's own, in every tab and
/// frame: JavaScript source, a file of it, or a function source the page
/// calls. A string converts to source.
/// </summary>
public sealed class WebInitScript
{
    private WebInitScript(string? source, string? path, string? function)
    {
        Source = source;
        Path = path;
        Function = function;
    }

    /// <summary>The JavaScript source, or <see langword="null"/> for a file or a function.</summary>
    public string? Source { get; }

    /// <summary>The file of JavaScript source, relative to the project root, or <see langword="null"/> for source or a function.</summary>
    public string? Path { get; }

    /// <summary>
    /// The source of a function the page calls, such as <c>() =&gt; { ... }</c>,
    /// or <see langword="null"/> for source or a file. It cannot close over test
    /// variables; <c>Browser.AddInitScriptAsync</c> passes it one JSON argument.
    /// </summary>
    public string? Function { get; }

    public static WebInitScript FromSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new WebInitScript(source, null, null);
    }

    public static WebInitScript FromPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new WebInitScript(null, path, null);
    }

    public static WebInitScript FromFunction(string function)
    {
        ArgumentNullException.ThrowIfNull(function);
        return new WebInitScript(null, null, function);
    }

    public static implicit operator WebInitScript(string source) => FromSource(source);

    /// <summary>The step label of this script (upstream's <c>initScriptLabel</c>): the file a path names, else its kind, never its source.</summary>
    internal string Label() => Path ?? (Function is null ? "source" : "function");

    /// <summary>Why this script cannot run, or <see langword="null"/>.</summary>
    internal string? Problem() => Path is { Length: 0 } ? "path must be a non-empty string" : null;

    /// <summary>
    /// The page source: the source itself, a function called with its JSON
    /// <paramref name="argument"/> (parsed in the page, since an object literal
    /// would turn an own <c>__proto__</c> key into the prototype), or the file's
    /// text with a <c>sourceURL</c>, so a stack trace in the page names it. A
    /// file that cannot be read throws what <paramref name="fail"/> makes of the problem.
    /// </summary>
    internal async Task<string> ReadAsync(string? argument, string projectRoot, Func<string, Exception, Exception> fail, CancellationToken cancellationToken)
    {
        if (Source is not null)
        {
            return Source;
        }

        if (Function is not null)
        {
            return "(" + Function + "\n)(" + (argument is null ? "" : "JSON.parse(" + System.Text.Json.JsonSerializer.Serialize(argument) + ")") + ");";
        }

        var file = Path!;
        try
        {
            file = System.IO.Path.GetFullPath(file, projectRoot);
            return await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false) + "\n//# sourceURL=" + file.ReplaceLineEndings("");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            var cause = ex is FileNotFoundException or DirectoryNotFoundException ? "ENOENT" : ex.Message;
            throw fail("path cannot be read: " + file + " (" + cause + ")", ex);
        }
    }
}

/// <summary>
/// Attach to a remote Chromium over CDP instead of launching one. Supplying
/// <see cref="ReconnectEndpoint"/> opts into a persistent context instead.
/// </summary>
public sealed class WebConnectOptions
{
    /// <summary>
    /// Resolves the CDP endpoint (a <c>ws://</c>, <c>wss://</c>, or <c>http://</c> DevTools URL).
    /// Called when each attempt starts, so a hosted endpoint provisioned per run can be used.
    /// With <see cref="ReconnectEndpoint"/>, each call must provision a fresh, dedicated browser.
    /// </summary>
    public required Func<CancellationToken, Task<string>> CdpEndpoint { get; init; }

    /// <summary>
    /// Opts into a dedicated persistent remote context. <see cref="CdpEndpoint"/> provisions
    /// a fresh browser at each attempt start; this resolver reconnects to that
    /// same browser after a transport drop. Called once before the next
    /// operation, within its budget. The original browser and page must survive.
    /// Dispatched operations are never retried. The host owns browser cleanup.
    /// Context replacement, headers, basicAuth, userAgent, locale, and timezoneId are unavailable in this mode.
    /// </summary>
    public Func<CancellationToken, Task<string>>? ReconnectEndpoint { get; init; }
}

/// <summary>Options of the browser engine: how it drives the app.</summary>
public sealed class WebEngineOptions
{
    /// <summary>Runs Chromium without a window. When unset, <c>E2E_HEADLESS=0</c> or <c>false</c> shows the window; headless otherwise.</summary>
    public bool? Headless { get; init; }

    /// <summary>
    /// Initial viewport of every attempt's page; default 1280 by 720. <see langword="null"/>
    /// emulates no size: the page fills the browser window, whatever size it has.
    /// </summary>
    public WebViewport? Viewport { get; init; } = WebViewport.Default;

    /// <summary>The attribute that carries an element's test id: what <c>GetByTestId</c> resolves and <see cref="SemanticNode.TestId"/> reports.</summary>
    public string TestIdAttribute { get; init; } = "data-testid";

    /// <summary>
    /// Headers added to every request bound for the app's host (the host of the
    /// session's base URL). Requests to any other host never carry them, so a
    /// secret header stays with the app it unlocks. Names are case-insensitive
    /// and replace a header the page already sends under the same name.
    /// Routing every request turns the browser's HTTP cache off.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Basic-auth credentials the browser answers a challenge with, wherever one is issued.</summary>
    public WebBasicAuth? BasicAuth { get; init; }

    /// <summary>The <c>User-Agent</c> the browser sends and <c>navigator.userAgent</c> reports. Defaults to the browser's own.</summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// The locale every attempt's context runs in, a BCP 47 tag such as <c>de-DE</c>:
    /// what <c>navigator.language</c>, <c>Intl</c> formatting, and the <c>Accept-Language</c>
    /// header report. Defaults to the browser's own. An <c>accept-language</c> entry in
    /// <see cref="Headers"/> beside it is <c>INVALID_CONFIG</c>.
    /// </summary>
    public string? Locale { get; init; }

    /// <summary>
    /// The IANA time zone every attempt's context runs in, such as <c>Europe/Berlin</c>:
    /// what <c>Date</c> and <c>Intl</c> resolve local time against. Defaults to the machine's.
    /// </summary>
    public string? TimezoneId { get; init; }

    /// <summary>
    /// The <c>prefers-color-scheme</c> every page reports from its first paint, to its <c>@media</c> rules and
    /// to <c>matchMedia</c>, as Playwright's own <c>colorScheme</c> context option sets it. Defaults to the
    /// browser's own. <c>Browser.EmulateMediaAsync</c> changes it for the rest of one attempt.
    /// </summary>
    public WebColorScheme? ColorScheme { get; init; }

    /// <summary>
    /// The <c>prefers-reduced-motion</c> every page reports from its first paint, as Playwright's own
    /// <c>reducedMotion</c> context option sets it: <see cref="WebReducedMotion.Reduce"/> lets an app's own rule
    /// turn its animations off, so none races an assertion. Defaults to the browser's own.
    /// <c>Browser.EmulateMediaAsync</c> changes it for the rest of one attempt.
    /// </summary>
    public WebReducedMotion? ReducedMotion { get; init; }

    /// <summary>Scripts every document runs before the page's own, in every tab and frame, in order.</summary>
    public IReadOnlyList<WebInitScript>? InitScripts { get; init; }

    /// <summary>
    /// How the engine's own screencast records an attempt that records video, when the browser provider does not record
    /// it. Which attempts record is the config's <c>video</c>.
    /// </summary>
    public WebScreencastOptions? Screencast { get; init; }

    /// <summary>
    /// The browser the engine launches: Chromium (the default), Firefox, or WebKit. .NET-only split of upstream's
    /// <c>browser</c> option, which is a name or a provider: the name is here and the provider is <see cref="Browser"/>.
    /// <see cref="Connect"/> and a provider attach over CDP, so they need Chromium.
    /// </summary>
    public BrowserName BrowserName { get; init; } = BrowserName.Chromium;

    /// <summary>Attach to a remote Chromium over CDP instead of launching a local one.</summary>
    public WebConnectOptions? Connect { get; init; }

    /// <summary>
    /// Leases hosted browsers from a provider instead of launching one: the engine attaches over CDP the way
    /// <see cref="Connect"/> does, and adds the lifecycle <see cref="Connect"/> lacks. It asks the provider for a browser
    /// when one is needed and gives it back when its scope ends, on every exit path. A provider implies Chromium and
    /// cannot be combined with <see cref="Connect"/>. With <see cref="BrowserProviderScope.Attempt"/> scope the attempt
    /// rides a persistent context, with the limits of <see cref="WebConnectOptions.ReconnectEndpoint"/>.
    /// </summary>
    public IBrowserProvider? Browser { get; init; }
}

/// <summary>
/// Browser engine for the web target. It drives Chromium, Firefox, or WebKit through Playwright,
/// reads a semantic tree (role, name, text, test id, state), and performs the
/// same node actions the document engine does. Install browsers once with
/// <c>playwright.ps1 install chromium</c> (or <c>firefox</c>, <c>webkit</c>) from this project's build output.
/// </summary>
public sealed partial class WebEngine : IEngine
{
    public const string EngineVersion = "1.1.0";

    private const string SkipInstallVariable = "E2E_SKIP_BROWSER_INSTALL";

    private const string SkipBrowserGcVariable = "PLAYWRIGHT_SKIP_BROWSER_GC";

    // Playwright's install is a no-op when the build is already there; one run per mode serves every session in the process.
    // One install per browser and window mode for the process: a download is never charged against a launch timeout or run twice.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(BrowserName Name, bool Headed), Lazy<Task<int>>> BrowserInstalls = new();

    private readonly WebEngineOptions _options;
    private readonly bool _headless;

    /// <summary>The configured headers, names lower-cased so they replace the browser's own; null when none are set.</summary>
    private readonly Dictionary<string, string>? _siteHeaders;

    // The default contexts earlier attempts rode, so a cdpEndpoint that hands one back is refused.
    // Process-wide: a test fixture creates an engine for each attempt, retries included.
    private static readonly HashSet<string> UsedContexts = new(StringComparer.Ordinal);

    public WebEngine(bool? headless = null)
        : this(new WebEngineOptions { Headless = headless })
    {
    }

    public WebEngine(WebEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);
        _options = options;
        _leases = options.Browser is { } provider ? new LeasedBrowsers(provider) : null;
        _siteHeaders = options.Headers is { Count: > 0 } headers
            ? headers.ToDictionary(pair => pair.Key.ToLowerInvariant(), pair => pair.Value, StringComparer.Ordinal)
            : null;
        if (options.Headless is bool chosen)
        {
            _headless = chosen;
        }
        else
        {
            var env = Environment.GetEnvironmentVariable("E2E_HEADLESS");
            _headless = !string.Equals(env, "0", StringComparison.Ordinal) && !string.Equals(env, "false", StringComparison.OrdinalIgnoreCase);
        }
    }

    public string Platform => "web";

    /// <summary>Attaches to a CDP endpoint; tests replace it to stand in for the remote browser.</summary>
    internal Func<IPlaywright, string, TimeSpan, CancellationToken, Task<IBrowser>> ConnectCdp { get; init; } = BrowserConnection.ConnectCdpAsync;

    /// <summary>Attaches to a Playwright-protocol endpoint (.NET-only); tests replace it to stand in for the remote browser.</summary>
    internal Func<IPlaywright, string, TimeSpan, CancellationToken, Task<IBrowser>> ConnectPlaywright { get; init; } = BrowserConnection.ConnectPlaywrightAsync;

    /// <summary>Starts the Playwright driver; tests replace it so a fake remote needs no driver process.</summary>
    internal Func<Task<IPlaywright>> CreatePlaywright { get; init; } = Playwright.CreateAsync;

    /// <summary>The clock operation budgets read; tests replace it to control elapsed time.</summary>
    internal TimeProvider Clock { get; init; } = TimeProvider.System;

    public string Version => EngineVersion;

    public EngineCapabilities Capabilities =>
        EngineCapabilities.Observation
        | EngineCapabilities.Actions
        | EngineCapabilities.Location
        | EngineCapabilities.Keyboard
        | EngineCapabilities.Scroll
        | EngineCapabilities.History
        | EngineCapabilities.Screenshot
        | EngineCapabilities.TapModifiers
        | EngineCapabilities.Video;

    /// <summary>
    /// A web target opens the app at <c>app.url</c>, so the URL is required, and the fields that describe an installed device app
    /// (<c>bundleId</c>, <c>appPath</c>, <c>launchArguments</c>) have nothing to act on. <c>app.permissions</c> must be ones the browser
    /// grants, and only grants. A provider or a CDP attach speaks CDP, so it takes Chromium's names. Upstream's <c>validateApp</c> in
    /// <c>web/src/engine.ts</c>.
    /// </summary>
    public void ValidateApp(AppConfig app, string targetName)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(targetName);
        foreach (var (key, declared) in new[]
        {
            ("bundleId", app.BundleId is not null),
            ("appPath", app.AppPath is not null),
            ("launchArguments", app.LaunchArguments is not null),
        })
        {
            if (declared)
            {
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    $"target \"{targetName}\" declares app.{key}, which describes an installed device app; a web() target opens app.url");
            }
        }

        if (app.Url is null)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"target \"{targetName}\" needs app.url: a web() target opens the app at a URL, targets: [{{ engine: web(), app: {{ url: 'http://localhost:3000' }} }}]");
        }

        if (app.Permissions is { } permissions)
        {
            WebPermissions.ValidatePermissions(targetName, PermissionBrowser(_options.Connect), permissions);
        }
    }

    // A provider or a CDP attach speaks CDP, so it takes Chromium's names.
    private BrowserName PermissionBrowser(WebConnectOptions? connect) =>
        connect is null && _options.Browser is null ? _options.BrowserName : BrowserName.Chromium;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        if (_leases is null)
        {
            return await StartSessionAsync(options, _options.Connect, UsedContexts, null, cancellationToken).ConfigureAwait(false);
        }

        // A leased browser is given back on every path: here when the start fails, and by the session when it ends.
        var lease = await OpenLeaseAsync(options, cancellationToken).ConfigureAwait(false);
        try
        {
            return await StartSessionAsync(options, lease.Connect, lease.UsedContexts, lease, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await lease.CloseAsync(connected: false).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            throw;
        }
    }

    private async Task<IEngineSession> StartSessionAsync(EngineStartOptions options, WebConnectOptions? connect, HashSet<string> usedContexts, LeaseScope? lease, CancellationToken cancellationToken)
    {
        try
        {
            var initScripts = await ReadInitScriptsAsync(options.ProjectRoot, cancellationToken).ConfigureAwait(false);

            // A provider or a CDP attach speaks CDP, so it takes Chromium's names.
            if (options.Permissions is { } permissions)
            {
                WebPermissions.ValidatePermissions(options.TargetName ?? "web", PermissionBrowser(connect), permissions);
            }

            if (connect is null)
            {
                await EnsureBrowserAsync(_options.BrowserName, !_headless, cancellationToken).ConfigureAwait(false);
            }

            var playwright = await CreatePlaywright().ConfigureAwait(false);
            var app = Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var parsed) ? parsed : null;
            var seams = new SessionSeams(Clock, ConnectCdp);
            if (connect is { ReconnectEndpoint: { } reconnect } persistent)
            {
                var persistentSession = await WebSession.StartPersistentAsync(
                    playwright,
                    new PersistentConnect(persistent.CdpEndpoint, reconnect, usedContexts, _options.Browser),
                    options.ActionTimeout,
                    _options,
                    initScripts,
                    context => SetUpContextAsync(context, app, options.Permissions),
                    url => SiteHeadersFor(url, app),
                    seams,
                    lease,
                    VideoSetupOf(options, lease),
                    cancellationToken).ConfigureAwait(false);
                return persistentSession.TraceTo(options);
            }

            IBrowser? browser = null;
            WebSession? session = null;
            try
            {
                await RegisterSelectorEnginesAsync(playwright).ConfigureAwait(false);
                var credentials = _options.BasicAuth is { } basicAuth
                    ? await HttpCredentialsAsync(basicAuth, options.ResolveSecret ?? ((secret, _) => Task.FromResult(secret.Value))).ConfigureAwait(false)
                    : null;
                if (connect is not null)
                {
                    var endpoint = await ResolveEndpointAsync(connect, cancellationToken).ConfigureAwait(false);

                    // As upstream, a connect that never answers is bounded by the browser launch budget.
                    var dial = lease?.Protocol == BrowserProtocol.Playwright ? ConnectPlaywright : ConnectCdp;
                    browser = await dial(playwright, endpoint, E2EDefaults.LaunchTimeout, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    browser = await BrowserConnection.BrowserType(playwright, _options.BrowserName).LaunchAsync(new BrowserTypeLaunchOptions { Headless = _headless }).ConfigureAwait(false);
                }

                session = new WebSession(playwright, browser, options.ActionTimeout, _options, initScripts, context => SetUpContextAsync(context, app, options.Permissions), url => SiteHeadersFor(url, app), seams, credentials: credentials, lease: lease) { Video = VideoSetupOf(options, lease), ArtifactsDir = options.ArtifactsDir };
                session.TraceTo(options);
                await session.NewContextAsync().ConfigureAwait(false);

                return session;
            }
            catch
            {
                // Without a session nothing owns the browser or the driver yet, so release them before the start fails.
                if (session is not null)
                {
                    await session.DisposeAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    throw;
                }

                if (browser is not null)
                {
                    await browser.CloseAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                }

                try
                {
                    playwright.Dispose();
                }
#pragma warning disable CA1031 // A driver that fails to stop must not hide why the start failed.
                catch (Exception)
#pragma warning restore CA1031
                {
                }

                throw;
            }
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.Ordinal) || ex.Message.Contains("browserType.launch", StringComparison.Ordinal))
        {
            throw new EngineException(
                "ENVIRONMENT_UNAVAILABLE",
                _options.BrowserName + " is not installed for Playwright. Unset " + SkipInstallVariable + " to install it on launch, or from the build output run: playwright.ps1 install " + BrowserConnection.NameOf(_options.BrowserName),
                ex);
        }
        catch (Exception ex) when (WebErrors.IsPlaywright(ex))
        {
            throw new EngineException(EngineErrorCodes.EngineFailure, "browser launch failed: " + WebErrors.Message(ex), ex);
        }
    }

    /// <summary>
    /// Registers the engine's selector engines before any context exists, so every context the driver creates carries
    /// them. Every secure-field mask needs them.
    /// </summary>
    private static Task RegisterSelectorEnginesAsync(IPlaywright playwright) =>
        playwright.Selectors.RegisterAsync(PageScript.ClosedShadowSelectorEngine, new SelectorsRegisterOptions { Script = "(" + PageScript.ClosedShadowSelectorEngineSource + ")()" });

    /// <summary>
    /// Asks <c>connect.cdpEndpoint</c> for the endpoint to attach to. A resolver that is still pending when
    /// <paramref name="cancellationToken"/> fires is abandoned; one that throws or yields nothing fails the start.
    /// </summary>
    private static async Task<string> ResolveEndpointAsync(WebConnectOptions connect, CancellationToken cancellationToken)
    {
        string endpoint;
        try
        {
            endpoint = await connect.CdpEndpoint(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or EngineException))
        {
            throw new EngineException(EngineErrorCodes.EngineFailure, "browser launch failed: connect.cdpEndpoint failed: " + ex.Message, ex);
        }

        return string.IsNullOrWhiteSpace(endpoint)
            ? throw new EngineException(EngineErrorCodes.EngineFailure, "connect.cdpEndpoint resolved to an empty CDP endpoint")
            : endpoint;
    }

    /// <summary>True when <paramref name="url"/> is bound for the app's host, the only requests the configured headers ride.</summary>
    internal static bool IsAppRequest(string url, Uri app)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var target)
            && (target.Scheme == Uri.UriSchemeHttp || target.Scheme == Uri.UriSchemeHttps)
            && string.Equals(target.Host, app.Host, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The <c>playwright install</c> arguments for Chromium: <c>--only-shell</c> on a headless run, which launches the headless shell.</summary>
    internal static string[] InstallArgs(bool headed)
    {
        return InstallArgs([BrowserName.Chromium], headed);
    }

    /// <summary>The <c>playwright install</c> arguments for <paramref name="names"/> (upstream <c>installArgs</c>): <c>--only-shell</c> on a headless run that installs Chromium.</summary>
    internal static string[] InstallArgs(IReadOnlyList<BrowserName> names, bool headed)
    {
        var browsers = names.Select(BrowserConnection.NameOf);
        return headed || !names.Contains(BrowserName.Chromium) ? [.. browsers] : ["--only-shell", .. browsers];
    }

    /// <summary>
    /// Runs <paramref name="install"/> with the Chromium install arguments and <c>PLAYWRIGHT_SKIP_BROWSER_GC=1</c>
    /// unless the user set it, so the install keeps other tools' browsers.
    /// </summary>
    internal static int RunChromiumInstall(bool headed, Func<string[], int> install)
    {
        return RunBrowserInstall(BrowserName.Chromium, headed, install);
    }

    /// <summary>Runs <paramref name="install"/> with the install arguments for <paramref name="name"/>, as <see cref="RunChromiumInstall"/> does for Chromium.</summary>
    internal static int RunBrowserInstall(BrowserName name, bool headed, Func<string[], int> install)
    {
        return RunPlaywrightCli(["install", .. InstallArgs([name], headed)], install);
    }

    /// <summary>
    /// Runs the Playwright CLI with <paramref name="args"/> and returns its exit code (upstream <c>runPlaywrightCli</c>), keeping
    /// other tools' browsers as <see cref="RunChromiumInstall"/> does. <paramref name="playwright"/> is a test seam.
    /// Upstream spawns <c>node cli.js</c> and forwards SIGINT and SIGTERM to it; <c>Program.Main</c> runs in this process.
    /// </summary>
    internal static int RunPlaywrightCli(string[] args, Func<string[], int>? playwright = null)
    {
        // Program.Main starts the driver with this process's environment, so the value is set on the process.
        Environment.SetEnvironmentVariable(SkipBrowserGcVariable, Environment.GetEnvironmentVariable(SkipBrowserGcVariable) ?? "1");
        return (playwright ?? Microsoft.Playwright.Program.Main)(args);
    }

    internal static Task EnsureChromiumAsync(bool headed, CancellationToken cancellationToken)
    {
        return EnsureBrowserAsync(BrowserName.Chromium, headed, cancellationToken);
    }

    /// <summary>Installs <paramref name="name"/> once per process unless <see cref="SkipInstallVariable"/> is set (upstream <c>ensureBrowsersInstalled</c>).</summary>
    internal static async Task EnsureBrowserAsync(BrowserName name, bool headed, CancellationToken cancellationToken)
    {
        var skip = Environment.GetEnvironmentVariable(SkipInstallVariable);
        if (string.Equals(skip, "1", StringComparison.Ordinal) || string.Equals(skip, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var install = BrowserInstalls.GetOrAdd((name, headed), key => new Lazy<Task<int>>(() => InstallBrowserAsync(key)));
        var exitCode = await install.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new EngineException(
                "ENVIRONMENT_UNAVAILABLE",
                "Playwright could not install " + BrowserConnection.NameOf(name) + " (exit code " + exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + "). Install it yourself and set " + SkipInstallVariable + "=1 to skip this step.");
        }
    }

    /// <summary>
    /// Runs the install behind <see cref="BrowserInstalls"/>. A failed install removes its own entry here, not in a waiter, so it
    /// is not cached even when every waiter was cancelled before it finished: a later session retries it.
    /// </summary>
    private static async Task<int> InstallBrowserAsync((BrowserName Name, bool Headed) key)
    {
        var failed = true;
        try
        {
            var exitCode = await Task.Run(() => RunBrowserInstall(key.Name, key.Headed, Microsoft.Playwright.Program.Main)).ConfigureAwait(false);
            failed = exitCode != 0;
            return exitCode;
        }
        finally
        {
            if (failed)
            {
                BrowserInstalls.TryRemove(key, out _);
            }
        }
    }

    private static void Validate(WebEngineOptions options)
    {
        if (options.Browser is { } browser)
        {
            ValidateProvider(browser, options.Connect);
        }

        if ((options.Connect is not null || options.Browser is not null) && options.BrowserName != BrowserName.Chromium)
        {
            var source = options.Connect is not null ? "WebEngineOptions.Connect" : "WebEngineOptions.Browser";
            throw new EngineException("INVALID_CONFIG", source + " requires the chromium browser; CDP attach is chromium-only, got \"" + BrowserConnection.NameOf(options.BrowserName) + "\".");
        }

        if (!AttributeNamePattern().IsMatch(options.TestIdAttribute ?? ""))
        {
            throw new EngineException("INVALID_CONFIG", "testIdAttribute must be an attribute name such as \"data-testid\", got \"" + options.TestIdAttribute + "\".");
        }

        if (options.Viewport is { } viewport && (viewport.Width <= 0 || viewport.Height <= 0))
        {
            throw new EngineException("INVALID_CONFIG", "viewport width and height must be positive.");
        }

        foreach (var (name, value) in options.Headers ?? new Dictionary<string, string>())
        {
            if (!ProtectedApp.HeaderNamePattern().IsMatch(name))
            {
                throw new EngineException("INVALID_CONFIG", "headers has an invalid header name: \"" + name + "\".");
            }

            if (ProtectedApp.FieldValueControlPattern().IsMatch(value))
            {
                throw new EngineException("INVALID_CONFIG", "headers: header \"" + name + "\" must not contain a control character.");
            }
        }

        if (options.BasicAuth is { } auth)
        {
            if (auth.Username.Length == 0)
            {
                throw new EngineException("INVALID_CONFIG", "basicAuth requires a non-empty username string.");
            }

            if (auth.Username.Contains(':', StringComparison.Ordinal))
            {
                throw new EngineException("INVALID_CONFIG", "basicAuth username must not contain \":\".");
            }
        }

        if (options.UserAgent is { } userAgent)
        {
            if (userAgent.Length == 0)
            {
                throw new EngineException("INVALID_CONFIG", "userAgent must be a non-empty string.");
            }

            if (ProtectedApp.FieldValueControlPattern().IsMatch(userAgent))
            {
                throw new EngineException("INVALID_CONFIG", "userAgent must not contain a control character.");
            }

            if (options.Headers is { } headers
                && headers.Keys.Any(name => string.Equals(name, "user-agent", StringComparison.OrdinalIgnoreCase)))
            {
                throw new EngineException("INVALID_CONFIG", "userAgent and a user-agent header in headers conflict; set userAgent only.");
            }
        }

        if (options.Locale is { } locale)
        {
            ValidateLocale(locale, options.Headers);
        }

        if (options.TimezoneId is { } timezoneId && !IsTimeZone(timezoneId))
        {
            throw new EngineException("INVALID_CONFIG", "timezoneId must be an IANA time zone such as \"Europe/Berlin\", got \"" + timezoneId + "\".");
        }

        ValidateMedia(options);
        ValidateScreencast(options.Screencast);

        for (var index = 0; index < (options.InitScripts?.Count ?? 0); index++)
        {
            var script = options.InitScripts![index];
            if ((script is null ? "must be a string of source, a { path }, or a function, got null" : script.Problem()) is { } problem)
            {
                throw new EngineException("INVALID_CONFIG", InitScriptAt(index) + problem + ".");
            }
        }

        // The options applied when the engine creates a browser context, which a persistent context never is.
        // A per-attempt lease rides the same persistent context as reconnectEndpoint, with the same limits.
        if (options.Connect?.ReconnectEndpoint is not null || options.Browser?.Scope == BrowserProviderScope.Attempt)
        {
            var creationOptions = new (string Name, bool Set)[]
            {
                ("headers", options.Headers is not null),
                ("basicAuth", options.BasicAuth is not null),
                ("userAgent", options.UserAgent is not null),
                ("locale", options.Locale is not null),
                ("timezoneId", options.TimezoneId is not null),
            }.Where(option => option.Set).Select(option => option.Name).ToList();
            if (creationOptions.Count > 0)
            {
                throw new EngineException(
                    "INVALID_CONFIG",
                    PersistentMode(options.Browser) + " uses a persistent context; " + string.Join(", ", creationOptions) + (creationOptions.Count == 1 ? " requires" : " require") + " a newly created context");
            }
        }
    }

    /// <summary><c>web({ Screencast })</c>: a whole-pixel frame size of at least 1 and a 0-100 JPEG quality, each optional.</summary>
    private static void ValidateScreencast(WebScreencastOptions? screencast)
    {
        if (screencast?.Size is { } size && (size.Width < 1 || size.Height < 1))
        {
            throw new EngineException("INVALID_CONFIG", "web({ Screencast: { Size } }) must be { Width, Height } in whole pixels, each at least 1");
        }

        if (screencast?.Quality is < 0 or > 100)
        {
            throw new EngineException("INVALID_CONFIG", "web({ Screencast: { Quality } }) must be an integer from 0 through 100");
        }
    }

    /// <summary>What puts an attempt on a persistent context, as an error message names it.</summary>
    private static string PersistentMode(IBrowserProvider? provider) =>
        provider is null ? "connect.reconnectEndpoint" : $"browser provider \"{provider.Name}\" with scope \"attempt\"";

    /// <summary>Refuses a provider the engine could not lease from, at config load: what an interface cannot say for itself.</summary>
    private static void ValidateProvider(IBrowserProvider provider, WebConnectOptions? connect)
    {
        if (string.IsNullOrWhiteSpace(provider.Name))
        {
            throw new EngineException("INVALID_CONFIG", "a browser provider needs a non-empty Name.");
        }

        if (connect is not null)
        {
            throw new EngineException(
                "INVALID_CONFIG",
                $"Browser and Connect name two browser sources; browser provider \"{provider.Name}\" leases its own browsers, so remove Connect.");
        }

        if (!Enum.IsDefined(provider.Scope))
        {
            throw new EngineException(
                "INVALID_CONFIG",
                $"browser provider \"{provider.Name}\" has scope {(int)provider.Scope}; use Worker or Attempt.");
        }

        if (provider.Downloads is { } downloads)
        {
            if (string.IsNullOrWhiteSpace(downloads.Dir) || downloads.ReadAsync is null)
            {
                throw new EngineException("INVALID_CONFIG", $"browser provider \"{provider.Name}\" has downloads that are not a Dir and a ReadAsync.");
            }

            // The browser's own machine may be POSIX or Windows, whatever the runner's is.
            if (!IsAbsoluteOnEitherSystem(downloads.Dir))
            {
                throw new EngineException(
                    "INVALID_CONFIG",
                    $"browser provider \"{provider.Name}\" has Downloads.Dir \"{downloads.Dir}\"; name an absolute path on the browser's machine.");
            }
        }
    }

    private static bool IsAbsoluteOnEitherSystem(string path) =>
        path.StartsWith('/') || path.StartsWith('\\') || (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '/' or '\\');

    // Refuses a media feature value the browser does not emulate, at config load rather than at the first page.
    // A persistent context takes these too: they are set on each page, not only on a context the engine creates.
    private static void ValidateMedia(WebEngineOptions options)
    {
        if (WebMedia.Problem(name => "web({ " + name + " })", new WebMedia { ColorScheme = options.ColorScheme, ReducedMotion = options.ReducedMotion }) is { } problem)
        {
            throw new EngineException("INVALID_CONFIG", problem);
        }
    }

    private static string InitScriptAt(int index) => "initScripts[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "] ";

    // The configured init scripts as page source, read before the browser
    // launches, so a missing file fails the attempt with nothing to close.
    private async Task<List<string>> ReadInitScriptsAsync(string? projectRoot, CancellationToken cancellationToken)
    {
        var sources = new List<string>();
        var scripts = _options.InitScripts ?? [];
        for (var index = 0; index < scripts.Count; index++)
        {
            var at = InitScriptAt(index);
            var root = System.IO.Path.GetFullPath(string.IsNullOrEmpty(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot);
            sources.Add(await scripts[index].ReadAsync(null, root, (message, cause) => new EngineException("INVALID_CONFIG", at + message, cause), cancellationToken).ConfigureAwait(false));
        }

        return sources;
    }

    /// <summary>The live page and context behind a web session, for tests that drive the browser out of band; null for any other session.</summary>
    internal static LiveSurface? SurfaceOf(IEngineSession session) =>
        session is WebSession web ? new LiveSurface(() => web.LivePage, () => web.LiveContext, web.ArtifactPath) : null;

    // Refuses a locale that is not a BCP 47 tag, and one an accept-language
    // header would override on the app's host while navigator.language kept
    // reporting it.
    private static void ValidateLocale(string locale, IReadOnlyDictionary<string, string>? headers)
    {
        if (LanguageTagPattern().Match(locale) is not { Success: true } tag
            || string.Equals(tag.Groups["language"].Value, "und", StringComparison.OrdinalIgnoreCase))
        {
            // "und" (undetermined) parses, but Chromium refuses it at context creation.
            throw new EngineException("INVALID_CONFIG", "locale must be a BCP 47 language tag such as \"de-DE\", got \"" + locale + "\".");
        }

        if (headers is not null && headers.Keys.Any(name => string.Equals(name, "accept-language", StringComparison.OrdinalIgnoreCase)))
        {
            throw new EngineException("INVALID_CONFIG", "locale and an accept-language header in headers conflict; set locale only.");
        }
    }

    // True when the name is an IANA time zone spelled with its own case. The
    // runtime lookup ignores case, and Chromium refuses a miscased name
    // (europe/berlin), so the spelling must also be one ICU knows or a tz
    // database file. Links (US/Eastern, GMT) are both.
    private static bool IsTimeZone(string timezoneId)
    {
        return TimeZoneInfo.TryFindSystemTimeZoneById(timezoneId, out var zone)
            && zone.HasIanaId
            && (TimeZoneInfo.TryConvertIanaIdToWindowsId(timezoneId, out _) || IsZoneInfoName(timezoneId));
    }

    // True when each segment of the name is an entry of the tz database
    // directory the runtime reads on Unix, matched with its own case. ICU
    // misses a few zones (EST5EDT), and is absent in invariant globalization.
    private static bool IsZoneInfoName(string timezoneId)
    {
        var directory = Environment.GetEnvironmentVariable("TZDIR") is { Length: > 0 } tzdir ? tzdir : "/usr/share/zoneinfo";
        var caseSensitive = new EnumerationOptions { MatchCasing = MatchCasing.CaseSensitive, MatchType = MatchType.Simple };
        foreach (var segment in timezoneId.Split('/'))
        {
            if (!Directory.Exists(directory) || !Directory.EnumerateFileSystemEntries(directory, segment, caseSensitive).Any())
            {
                return false;
            }

            directory = Path.Combine(directory, segment);
        }

        return true;
    }

    // An attribute name an element could carry, so a test id query can match something.
    [GeneratedRegex(@"^[A-Za-z_:][A-Za-z0-9_:.-]*\z")]
    private static partial Regex AttributeNamePattern();

    // A Unicode locale identifier, as Intl.Locale parses one: language, then
    // optional script, region, variants, extensions, and private use.
    [GeneratedRegex(@"^(?<language>[a-z]{2,3}|[a-z]{5,8})(-[a-z]{4})?(-([a-z]{2}|[0-9]{3}))?(-([a-z0-9]{5,8}|[0-9][a-z0-9]{3}))*(-[0-9a-wyz](-[a-z0-9]{2,8})+)*(-x(-[a-z0-9]{1,8})+)?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LanguageTagPattern();

    /// <summary>
    /// The basic-auth credentials a context answers a challenge with, as Playwright's own <c>HttpCredentials</c> does:
    /// on a <c>401</c> from any origin. A <see cref="Secret"/> password resolves through the attempt, which registers the
    /// value for redaction, and the <c>Authorization</c> credential Playwright sends with it too: a page that echoes its
    /// request headers shows that, not the password.
    /// </summary>
    internal static async Task<HttpCredentials> HttpCredentialsAsync(WebBasicAuth basicAuth, Func<Secret, ResolveSecretOptions?, Task<string>> resolveSecret)
    {
        var username = basicAuth.Username;
        if (basicAuth.Password is string password)
        {
            return new HttpCredentials { Username = username, Password = password };
        }

        var resolved = await resolveSecret((Secret)basicAuth.Password, new ResolveSecretOptions { Derived = plaintext => BasicCredentials(username, plaintext) }).ConfigureAwait(false);
        return new HttpCredentials { Username = username, Password = resolved };
    }

    /// <summary>
    /// The credential an <c>Authorization: Basic</c> header carries for <paramref name="username"/> and
    /// <paramref name="password"/>, base64 of <c>username:password</c> as Playwright encodes it, with and without
    /// its <c>=</c> padding: an echo may drop or escape the padding.
    /// </summary>
    internal static IReadOnlyList<string> BasicCredentials(string username, string password)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
        var unpadded = encoded.TrimEnd('=');
        return unpadded == encoded ? [encoded] : [encoded, unpadded];
    }

    // A clean context: the viewport the session holds now (setViewport
    // outlives restart and clearState), or no emulation when there is none.
    private static BrowserNewContextOptions ContextOptions(WebEngineOptions options, ViewportSize? viewport, HttpCredentials? credentials)
    {
        var context = new BrowserNewContextOptions
        {
            ViewportSize = viewport ?? ViewportSize.NoViewport,
            Locale = options.Locale,
            TimezoneId = options.TimezoneId,
        };

        // On the context too, so a tab the app opens itself starts with the configured values before the session reaches it.
        ConfiguredMedia(options).ApplyTo(context);
        if (options.UserAgent is not null)
        {
            context.UserAgent = options.UserAgent;
        }

        context.HttpCredentials = credentials;

        // Routing never sees a service worker's requests, so a worker would skip the headers.
        if (options.Headers is not null)
        {
            context.ServiceWorkers = ServiceWorkerPolicy.Block;
        }

        return context;
    }

    private static WebMedia ConfiguredMedia(WebEngineOptions options) => new() { ColorScheme = options.ColorScheme, ReducedMotion = options.ReducedMotion };

    /// <summary>
    /// The configured headers, names lower-cased, that a request to <paramref name="url"/>
    /// carries: all of them for the app's host, none for any other host or when there is no app.
    /// </summary>
    private IReadOnlyDictionary<string, string>? SiteHeadersFor(string url, Uri? app)
    {
        return app is not null && IsAppRequest(url, app) ? _siteHeaders : null;
    }

    /// <summary>
    /// What every context of the attempt is set up with before its first page: the site headers, then the permissions the target
    /// grants (upstream's <c>setUpContext</c> hook).
    /// </summary>
    private async Task SetUpContextAsync(IBrowserContext context, Uri? app, IReadOnlyDictionary<string, AppPermissionState>? permissions)
    {
        await InstallSiteHeadersAsync(context, app).ConfigureAwait(false);

        // ValidatePermissions admitted only grants.
        if (permissions is { Count: > 0 })
        {
            await context.GrantPermissionsAsync([.. permissions.Keys]).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds the configured headers to every request bound for the app's host, through a
    /// context route that falls back to the network. Registered before any test route,
    /// so it runs last: a test route's fallback reaches it, and a continue, which skips
    /// it, merges the same headers itself.
    /// </summary>
    private async Task InstallSiteHeadersAsync(IBrowserContext context, Uri? app)
    {
        if (_siteHeaders is not { } headers || app is null)
        {
            return;
        }

        await context.RouteAsync(url => IsAppRequest(url, app), async route =>
        {
            var merged = new Dictionary<string, string>(route.Request.Headers, StringComparer.Ordinal);
            foreach (var (name, value) in headers)
            {
                merged[name] = value;
            }

            try
            {
                await route.FallbackAsync(new RouteFallbackOptions { Headers = merged }).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // The page closed under the request; nothing waits for it.
            }
        }).ConfigureAwait(false);
    }

    private sealed partial class WebSession : IBrowserSession
    {
        // The fields the page script marks secure.
        private const string SecureFieldSelector = "input[type=password i], input[autocomplete=current-password]";

        // The same fields inside the closed shadow roots Playwright's own selectors stop at.
        private const string ClosedSecureFieldSelector = PageScript.ClosedShadowSelectorEngine + "=" + SecureFieldSelector;

        private readonly IPlaywright _playwright;
        private readonly TimeSpan _actionTimeout;
        private readonly string _testIdAttribute;
        private readonly HttpCredentials? _credentials;
        private readonly WebEngineOptions _options;
        private readonly Func<IBrowserContext, Task> _setUpContext;
        private readonly Func<string, IReadOnlyDictionary<string, string>?> _siteHeaders;
        private readonly SessionSeams _seams;
        private readonly PersistentConnect? _persistent;

        // The browser lease the session rides, given back once its connection closes.
        private readonly LeaseScope? _lease;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Lock _recoveryLock = new();

        // When an operation that reconnected first began: its budget, which every page call in it shares, started then.
        private readonly AsyncLocal<long?> _operationStart = new();

        // Attempt-scoped routes, registered on the context so they cover every page,
        // and registered again on each context ClearStateAsync opens or a reconnect attaches.
        private readonly List<(Func<string, bool> Matches, Func<IRoute, Task> PlaywrightHandler, Func<IBrowserRoute, Task> Handler)> _routes = [];

        // The attempt's init scripts, configured then added, applied to each context the session opens.
        private readonly List<string> _initScripts;
        private IBrowserContext? _context;
        // Errors raised where nobody awaits them (a dialog handler, a route handler) wait here and fail the next step that enters the session.
        private readonly ErrorLatch _latch = new();
        private readonly DialogRouter _dialogs;
        private int _artifactCounter;

        /// <summary>The attempt's artifact directory (<see cref="EngineStartOptions.ArtifactsDir"/>); null when the session started without one.</summary>
        public string? ArtifactsDir { get; init; }
        private IPage? _page;
        private readonly object _pageLock = new();
        private Task<IPage>? _pendingPage;
        private ViewportSize? _viewport;

        // The media features every page the attempt opens emulates: the configured ones, until EmulateMediaAsync changes them.
        private WebMedia _media;

        // The emulation each page got as it appeared, so the attempt's own page is ready before it shows.
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<IPage, Task> _emulated = new();
        private Dictionary<string, IFrame> _frames = new(StringComparer.Ordinal);
        private int _nextRef = 1;

        // The locator that matched each located node alone, the way upstream keeps a single match on the strict Playwright locator.
        // Keyed by the node a locate returned, so a node the observation listed (same ref) is never strict.
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<SemanticNode, LocatorExpression> _strict = new();
        private IBrowser _browser;

        // Persistent recovery: the remote default context and page the attempt rides,
        // the shared reconnect in flight, the failure that ended the attempt, and
        // whether an observation was captured since the last reconnect.
        private string? _contextId;
        private TargetIdentity? _target;
        private Task? _recovery;
        private Exception? _failure;
        private bool _observed = true;
        private bool _firstPage = true;
        private bool _disposed;

        public WebSession(IPlaywright playwright, IBrowser browser, TimeSpan actionTimeout, WebEngineOptions options, List<string> initScripts, Func<IBrowserContext, Task> setUpContext, Func<string, IReadOnlyDictionary<string, string>?> siteHeaders, SessionSeams seams, PersistentConnect? persistent = null, HttpCredentials? credentials = null, LeaseScope? lease = null)
        {
            _lease = lease;
            _dialogs = new DialogRouter(_latch);
            _playwright = playwright;
            _initScripts = initScripts;
            _browser = browser;
            _options = options;
            _setUpContext = setUpContext;
            _siteHeaders = siteHeaders;
            _seams = seams;
            _persistent = persistent;
            _testIdAttribute = options.TestIdAttribute;
            _credentials = credentials;
            _media = ConfiguredMedia(options);
            _viewport = options.Viewport is { } viewport ? new ViewportSize { Width = viewport.Width, Height = viewport.Height } : null;
            _actionTimeout = actionTimeout;
        }

        private IPage Page
        {
            get
            {
                ThrowPending();
                return _page ?? throw new EngineException(EngineErrorCodes.InvalidState, "no app page is open; call app.open() first");
            }
        }

        private float ActionMs => (float)_actionTimeout.TotalMilliseconds;

        internal IPage LivePage => Page;

        internal IBrowserContext LiveContext => RequireContext();

        public string Route => Routes.LocationOf(Page.Url);

        public Task OpenAsync(string url, CancellationToken cancellationToken) => ActedAsync(cancellationToken, () => RunAsync("navigation", cancellationToken, () => NavigatingAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await EnsurePageAsync().ConfigureAwait(false);

                // The budget is the navigation's: opening the first page does not spend it.
                var budget = Budget("navigate to " + url, cancellationToken);
                await budget.WithinAsync(Page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = budget.PlaywrightTimeout,
                })).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new EngineException(EngineErrorCodes.Cancelled, "navigate to " + url + " cancelled", retryable: false, ex);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.Translate(ex, "navigate to " + url);
            }

        })));

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => ObserveAsync(cancellationToken, null);

        // The least a locate's read is given, so the last poll of a wait at its deadline still reads the page and the wait fails on what it saw.
        private static readonly TimeSpan MinLocateRead = TimeSpan.FromMilliseconds(500);

        // A locate reads under what is left of its wait, never longer than the action timeout an observation has.
        private Task<Observation> ObserveAsync(CancellationToken cancellationToken, TimeSpan? timeout) => RunAsync("observe", cancellationToken, async () =>
        {
            var budget = Budget("observe", cancellationToken, timeout is { } left && left < _actionTimeout ? TimeSpan.FromMilliseconds(Math.Min(Math.Max(left.TotalMilliseconds, MinLocateRead.TotalMilliseconds), _actionTimeout.TotalMilliseconds)) : null);
            var walk = new FrameWalk();
            List<WebNode> roots;
            try
            {
                roots = await CollectAsync(Page.MainFrame, walk, budget).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.NavigationStaleOr(ex, "observe");
            }

            _frames = walk.Frames;
            _observed = true;
            return new Observation { Route = Route, Roots = roots.Select(node => ToNode(node, _testIdAttribute)).ToList(), Truncated = walk.Truncated, ScrollPosition = walk.Scroll, Viewport = walk.Viewport };
        });

        public bool CanLocate => true;

        /// <summary>
        /// Resolves a locator against the page as it is now, in the engine: it reads the semantic tree and resolves the expression over
        /// it, so every match is a node of that observation and keeps the ref the page script minted. Upstream's <c>locate</c>
        /// (<c>surface.ts</c>) resolves with Playwright selectors instead; the port's page script is the reader of the page, so the
        /// resolve is the tree's, and only a <c>selector</c> or <c>frame</c> expression asks Playwright which elements it names
        /// (<see cref="ProjectAsync(LocatorExpression, TimeSpan, CancellationToken)"/>). <paramref name="timeout"/> is the budget left for the
        /// wait: the read runs under it, or under the action timeout when that is shorter.
        /// </summary>
        public async Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorExpression expression, TimeSpan timeout, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(expression);
            var observation = await ObserveAsync(cancellationToken, timeout).ConfigureAwait(false);
            _trace?.ShowScreen(observation);
            var projected = WebLocators.NeedsPage(expression)
                ? await RunAsync("locate", cancellationToken, () => ProjectAsync(expression, timeout, cancellationToken)).ConfigureAwait(false)
                : expression;
            var matches = LocatorResolver.Resolve(observation, projected, expression.IncludeHidden);
            // As upstream's locate: a single match keeps the strict locator, so a node that turns ambiguous between locate and perform
            // is stale instead of acted on. A match of several is pinned to its own element, and so is every match of a predicate
            // query (display value, exact label), which upstream pins to the handles of the elements it read.
            if (matches.Count == 1 && !PinsToElements(expression))
            {
                _strict.AddOrUpdate(matches[0], expression);
            }

            return matches;
        }

        /// <summary>
        /// The expression with every <c>selector</c> and <c>frame</c> in it replaced by the nodes Playwright says it names, as <c>refs</c>:
        /// the elements the page script listed in the observation just taken, which is every element that has a ref. An element the tree
        /// leaves out (no role, test id, or text of its own) matches nothing, and so does the content of a frame it does not show.
        /// Upstream's <c>project</c> (<c>locators.ts</c>) and <c>frameBox</c> (<c>surface.ts</c>), over the tree.
        /// </summary>
        private async Task<LocatorExpression> ProjectAsync(LocatorExpression expression, TimeSpan timeout, CancellationToken cancellationToken)
        {
            WebLocators.FrameSelectors(expression);
            var budget = Budget("locate", cancellationToken, timeout < _actionTimeout ? TimeSpan.FromMilliseconds(Math.Max(timeout.TotalMilliseconds, MinLocateRead.TotalMilliseconds)) : null);
            try
            {
                return await ProjectAsync(expression, budget).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.StaleOr(ex, "locate");
            }
        }

        private async Task<LocatorExpression> ProjectAsync(LocatorExpression expression, OperationBudget budget)
        {
            switch (expression.Kind)
            {
                case "union":
                    return expression with { Left = await ProjectAsync(expression.Left!, budget).ConfigureAwait(false), Right = await ProjectAsync(expression.Right!, budget).ConfigureAwait(false) };
                case "filter":
                    return expression with
                    {
                        Source = await ProjectAsync(expression.Source!, budget).ConfigureAwait(false),
                        Has = expression.Has is null ? null : await ProjectAsync(WithinSource(expression.Has, expression.Source!, "has"), budget).ConfigureAwait(false),
                        HasNot = expression.HasNot is null ? null : await ProjectAsync(WithinSource(expression.HasNot, expression.Source!, "hasNot"), budget).ConfigureAwait(false),
                    };
                case "index":
                    return expression with { Source = await ProjectAsync(expression.Source!, budget).ConfigureAwait(false) };
                case "selector":
                    return Named(await RefsOfAsync([], expression.Selector!, budget).ConfigureAwait(false));
                case "frame":
                    {
                        var frames = new List<string>();
                        var leaf = expression;
                        while (leaf.Kind == "frame")
                        {
                            frames.Add(leaf.Selector!);
                            leaf = leaf.Source!;
                        }

                        var frame = await FrameAsync(frames, budget).ConfigureAwait(false);
                        return leaf.Kind switch
                        {
                            "selector" => Named(await RefsOfAsync(frames, leaf.Selector!, budget).ConfigureAwait(false)),
                            "role" or "text" or "label" or "testid" or "placeholder" or "displayValue" when leaf.Parent is null => leaf with { Parent = Named([frame]) },
                            _ => throw new EngineException("UNSUPPORTED_CAPABILITY", "a frame scopes a query or a selector, not " + leaf.Kind, retryable: false),
                        };
                    }

                default:
                    return expression.Parent is null ? expression : expression with { Parent = await ProjectAsync(expression.Parent, budget).ConfigureAwait(false) };
            }
        }

        /// <summary>
        /// A <c>has</c> filter resolves inside each match of its source, so a frame the two share is already the source's: the frame's
        /// iframe would otherwise scope the filter to descendants of the iframe, which the match is not one of. A <c>has</c> in another
        /// document cannot be joined to its source.
        /// </summary>
        private static LocatorExpression WithinSource(LocatorExpression has, LocatorExpression source, string key)
        {
            if (!WebLocators.FrameSelectors(has).SequenceEqual(WebLocators.FrameSelectors(source), StringComparer.Ordinal))
            {
                throw new EngineException("UNSUPPORTED_CAPABILITY", $"locator.filter({{ {key} }}) requires both locators in the same document", retryable: false);
            }

            return WebLocators.WithoutFrameScopes(has);
        }

        private static LocatorExpression Named(IEnumerable<string> refs) => new() { Kind = "refs", Refs = new HashSet<string>(refs, StringComparer.Ordinal) };

        /// <summary>
        /// Checks that every frame selector along <paramref name="frames"/> matches exactly one element, each counted inside the frame
        /// before it (upstream's <c>frameBox</c>; a frame-scoped count is 0 until that frame's document has loaded, which is what makes
        /// <c>FRAME_NOT_FOUND</c> worth retrying), and returns the ref the innermost iframe node has in the observation. An iframe the
        /// tree does not list, because it is hidden, is no frame either.
        /// </summary>
        private async Task<string> FrameAsync(IReadOnlyList<string> frames, OperationBudget budget)
        {
            ILocator? element = null;
            IFrameLocator? scope = null;
            foreach (var selector in frames)
            {
                element = scope is null ? Page.Locator(selector) : scope.Locator(selector);
                var count = await budget.WithinAsync(element.CountAsync()).ConfigureAwait(false);
                if (count == 0)
                {
                    throw new EngineException(EngineErrorCodes.FrameNotFound, $"no frame matches {selector}", retryable: true);
                }

                if (count > 1)
                {
                    throw new EngineException(EngineErrorCodes.FrameAmbiguous, $"{count} frames match {selector}", retryable: false);
                }

                scope = scope is null ? Page.FrameLocator(selector) : scope.FrameLocator(selector);
            }

            // A selector counts one element; only an iframe is a frame boundary.
            if (!await budget.WithinAsync(element!.EvaluateAsync<bool>(PageScript.IsFrameElement, null, new LocatorEvaluateOptions { Timeout = budget.PlaywrightTimeout })).ConfigureAwait(false))
            {
                throw new EngineException(EngineErrorCodes.FrameNotFound, $"{frames[^1]} matches an element that is not an iframe", retryable: true);
            }

            var node = await budget.WithinAsync(element.EvaluateAsync<string?>(PageScript.RefOf, null, new LocatorEvaluateOptions { Timeout = budget.PlaywrightTimeout })).ConfigureAwait(false);
            return node ?? throw new EngineException(EngineErrorCodes.FrameNotFound, $"frame {frames[^1]} is not in the observation: it is hidden, or the page changed after it", retryable: true);
        }

        /// <summary>The refs of the nodes the elements <paramref name="selector"/> names have, inside the frame the chain names or the page.</summary>
        private async Task<List<string>> RefsOfAsync(IReadOnlyList<string> frames, string selector, OperationBudget budget)
        {
            WebLocators.RefuseCapture(selector);
            IFrameLocator? scope = null;
            foreach (var frame in frames)
            {
                scope = scope is null ? Page.FrameLocator(frame) : scope.FrameLocator(frame);
            }

            var locator = scope is null ? Page.Locator(selector) : scope.Locator(selector);
            // One round trip for every match: an element the observation did not list has no ref, and is no node.
            var found = await budget.WithinAsync(locator.EvaluateAllAsync<string?[]>("els => els.map(" + PageScript.RefOf.Trim() + ")")).ConfigureAwait(false);
            return found.OfType<string>().ToList();
        }

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => ActedAsync(cancellationToken, () => RunAsync("perform", cancellationToken, async () =>
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(action);
            RequireObservation();
            var budget = Budget(action.GetType().Name.ToLowerInvariant(), cancellationToken);
            await RequireNoNewTwinAsync(node, budget).ConfigureAwait(false);
            var frame = _frames.GetValueOrDefault(node.Ref) ?? Page.MainFrame;
            if (frame.IsDetached)
            {
                throw new EngineException(EngineErrorCodes.NodeStale, $"Node {node.Ref} is not on the page. Observe again.", retryable: true);
            }

            IJSHandle handle;
            try
            {
                handle = await budget.WithinAsync(frame.EvaluateHandleAsync(PageScript.Find, node.Ref)).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.NavigationStaleOr(ex, "locate " + node.Ref);
            }
            catch (EngineException ex) when (ex.Code == EngineErrorCodes.OperationTimeout)
            {
                // As upstream, the deadline cutting off any part of an action is the action's cut-off.
                throw WebErrors.ClassifyAction(ex, action);
            }

            // A frame's handle to a missing ref is a plain JSHandle, which
            // AsElement rejects instead of returning null.
            var element = handle as IElementHandle;
            if (element is null)
            {
                await handle.DisposeAsync().ConfigureAwait(false);
                throw new EngineException(EngineErrorCodes.NodeStale, $"Node {node.Ref} is not on the page. Observe again.", retryable: true);
            }

            await using var disposeElement = element.ConfigureAwait(false);
            try
            {
                await budget.WithinAsync(DispatchAsync(element, action, budget)).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex) || ex is EngineException { Code: EngineErrorCodes.OperationTimeout })
            {
                throw WebErrors.ClassifyAction(ex, action);
            }
        }));

        public Task PressAsync(string key, CancellationToken cancellationToken) => ActedAsync(cancellationToken, () => RunAsync("keyboard.press", cancellationToken, () =>
        {
            RequireObservation();
            return InputAsync(() => Page.Keyboard.PressAsync(key), "press " + key, cancellationToken);
        }));

        // Direct test input: it needs no observation, so it stays available after a reconnect.
        public Task KeyboardPressAsync(string key, CancellationToken cancellationToken) => RunAsync("keyboard.press", cancellationToken, () =>
            InputAsync(() => Page.Keyboard.PressAsync(key), "press " + key, cancellationToken));

        public Task BackAsync(CancellationToken cancellationToken) => ActedAsync(cancellationToken, () => RunAsync("back", cancellationToken, () => NavigatingAsync(async () =>
        {
            var budget = Budget("back", cancellationToken);
            try
            {
                await budget.WithinAsync(Page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.Load, Timeout = budget.PlaywrightTimeout })).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.Translate(ex, "back");
            }

        })));

        public Task ForwardAsync(CancellationToken cancellationToken) => RunAsync("forward", cancellationToken, () => NavigatingAsync(async () =>
        {
            var budget = Budget("forward", cancellationToken);
            try
            {
                await budget.WithinAsync(Page.GoForwardAsync(new PageGoForwardOptions { WaitUntil = WaitUntilState.Load, Timeout = budget.PlaywrightTimeout })).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.Translate(ex, "forward");
            }
        }));

        public Task GotoAsync(string url, GotoWaitUntil waitUntil, TimeSpan timeout, CancellationToken cancellationToken) => RunAsync("goto", cancellationToken, () => NavigatingAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await EnsurePageAsync().ConfigureAwait(false);

                var budget = Budget("goto " + url, cancellationToken, timeout);
                var until = waitUntil switch
                {
                    GotoWaitUntil.DomContentLoaded => WaitUntilState.DOMContentLoaded,
                    GotoWaitUntil.NetworkIdle => WaitUntilState.NetworkIdle,
                    _ => WaitUntilState.Load,
                };
                await budget.WithinAsync(Page.GotoAsync(url, new PageGotoOptions { WaitUntil = until, Timeout = budget.PlaywrightTimeout })).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new EngineException(EngineErrorCodes.Cancelled, "goto " + url + " cancelled", retryable: false, ex);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.Translate(ex, "goto " + url);
            }
        }));

        public Task ReloadAsync(CancellationToken cancellationToken) => RunAsync("reload", cancellationToken, () => NavigatingAsync(async () =>
        {
            var budget = Budget("reload", cancellationToken);
            try
            {
                await budget.WithinAsync(Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Load, Timeout = budget.PlaywrightTimeout })).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.Translate(ex, "reload");
            }
        }));

        public Task RestartAsync(CancellationToken cancellationToken) => RunAsync("restart", cancellationToken, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = RequireContext();
            await VideoPageClosingAsync().ConfigureAwait(false);
            _page = null;
            foreach (var page in context.Pages.ToList())
            {
                await page.CloseAsync().ConfigureAwait(false);
            }

            await NewPageAsync(context).ConfigureAwait(false);
        });

        public async Task ClearStateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_persistent is not null)
            {
                // A per-attempt lease starts every attempt on a fresh browser already, and worker scope has the reset.
                var remedy = _persistent.Provider is null ? "" : "; every attempt already starts on a fresh browser, and scope \"worker\" can clear one mid-test";
                throw new EngineException(
                    EngineErrorCodes.UnsupportedCapability,
                    $"app.clearState() is unavailable with {PersistentMode(_persistent.Provider)}: it replaces the browser context, and the attempt rides one persistent context{remedy}");
            }

            ThrowPending();
            await VideoPageClosingAsync().ConfigureAwait(false);
            var context = _context;
            _context = null;
            _page = null;
            if (context is not null)
            {
                await context.CloseAsync().ConfigureAwait(false);
            }

            await NewContextAsync().ConfigureAwait(false);
            await NewPageAsync(_context!).ConfigureAwait(false);
        }

        public Task<string> GetUrlAsync(CancellationToken cancellationToken) => RunAsync("url", cancellationToken, () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Page.Url);
        });

        public Task<string> GetTitleAsync(CancellationToken cancellationToken) => RunAsync("title", cancellationToken, () =>
        {
            return Budget("title", cancellationToken).WithinAsync(Page.TitleAsync());
        });

        public Task<System.Text.Json.JsonElement?> EvaluateAsync(string expression, object? arg, bool hasArg, CancellationToken cancellationToken) => RunAsync<System.Text.Json.JsonElement?>("evaluate", cancellationToken, async () =>
        {
            var budget = Budget("evaluate", cancellationToken);
            System.Text.Json.JsonElement? outcome;
            try
            {
                outcome = await budget.WithinAsync(Page.EvaluateAsync<System.Text.Json.JsonElement?>(Evaluation.CompileEvaluation(expression, hasArg), arg)).ConfigureAwait(false);
            }
            catch (PlaywrightException ex) when (ex.Message.StartsWith("SyntaxError", StringComparison.Ordinal))
            {
                throw new TestException("EVALUATE_FAILED", PageMessage(ex), ex);
            }
            catch (PlaywrightException ex)
            {
                // A failure of the browser, not of the page code: the page code's own exceptions come back as a result.
                throw WebErrors.Translate(ex, "evaluate");
            }
            catch (ArgumentException ex)
            {
                // Playwright cannot carry a result that is not JSON, such as Infinity or NaN.
                throw new TestException("INVALID_ARGUMENT", "evaluate argument or result is not JSON: " + ex.Message, ex);
            }

            var result = outcome!.Value;
            if (result.GetProperty("ok").GetBoolean())
            {
                return result.TryGetProperty("value", out var value) ? value : default(System.Text.Json.JsonElement?);
            }

            throw new TestException("EVALUATE_FAILED", result.GetProperty("message").GetString()!);
        });

        // What the page code threw, without the stack Playwright appends or the "Error: " it prefixes to an Error's message.
        private static string PageMessage(PlaywrightException ex)
        {
            var message = ex.Message;
            var stack = message.IndexOf("\n    at ", StringComparison.Ordinal);
            if (stack >= 0)
            {
                message = message[..stack];
            }

            return message.StartsWith("Error: ", StringComparison.Ordinal) ? message["Error: ".Length..] : message;
        }

        public Task<BrowserResponse> WaitForResponseAsync(Func<string, bool> matches, TimeSpan timeout, CancellationToken cancellationToken) => RunAsync("waitForResponse", cancellationToken, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            IResponse response;
            try
            {
                // Before the first page exists, the wait listens on the context, which needs no page and
                // starts listening in this call, so a navigation started right after it cannot get ahead.
                response = await (_page is { IsClosed: false }
                    ? Page.WaitForResponseAsync(
                        candidate => matches(candidate.Url),
                        new PageWaitForResponseOptions { Timeout = (float)timeout.TotalMilliseconds })
                    : WaitForContextResponseAsync(matches, timeout, cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.Translate(ex, "waitForResponse");
            }

            // Started now, while the browser still holds the body, and awaited
            // only by the caller's body read on a budget of its own: the timeout
            // bounds the match, never a body still streaming in behind headers
            // that already arrived.
            return new BrowserResponse
            {
                Url = response.Url,
                Status = response.Status,
                Headers = response.Headers,
                Body = ReadBodyAsync(response),
            };
        });

        /// <summary>Waits for a response on any page of the context. It listens from the call on, so no page has to exist yet.</summary>
        private Task<IResponse> WaitForContextResponseAsync(Func<string, bool> matches, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var context = RequireContext();
            var found = new TaskCompletionSource<IResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<IResponse> listener = (_, candidate) =>
            {
                if (matches(candidate.Url))
                {
                    found.TrySetResult(candidate);
                }
            };
            context.Response += listener;
            return Wait();

            async Task<IResponse> Wait()
            {
                try
                {
                    return await found.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    throw new TimeoutException($"Timeout {timeout.TotalMilliseconds}ms exceeded while waiting for event \"Response\"");
                }
                finally
                {
                    context.Response -= listener;
                }
            }
        }

        public Task RouteAsync(Func<string, bool> matches, Func<IBrowserRoute, Task> handler, CancellationToken cancellationToken) => RunAsync("route", cancellationToken, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = RequireContext();
            // Tracked so the attempt end waits for a handler still running and collects what it latched.
            Func<IRoute, Task> playwrightHandler = route =>
            {
                var work = HandleRouteAsync(route, handler);
                _latch.Track("route", work);
                return work;
            };
            await context.RouteAsync(matches, playwrightHandler).ConfigureAwait(false);
            _routes.Add((matches, playwrightHandler, handler));
        });

        public Task UnrouteAsync(Func<IBrowserRoute, Task> handler, CancellationToken cancellationToken) => RunAsync("unroute", cancellationToken, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = RequireContext();
            var index = _routes.FindIndex(stored => stored.Handler == handler);
            if (index == -1)
            {
                return;
            }

            var (matches, playwrightHandler, _) = _routes[index];
            _routes.RemoveAt(index);
            await context.UnrouteAsync(matches, playwrightHandler).ConfigureAwait(false);
        });

        public Task<Action> OnDialogAsync(DialogHandler handler, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_dialogs.Add(handler));
        }

        /// <summary>
        /// Reserves one artifact path under the attempt directory: a per-attempt counter keeps names unique, the label keeps them
        /// readable (upstream's <c>artifactPath</c>).
        /// </summary>
        internal (string Relative, string Absolute) ArtifactPath(string folder, string? label, string extension)
        {
            var root = ArtifactsDir ?? throw new EngineException(EngineErrorCodes.InvalidState, "artifacts need the attempt's artifact directory; start the session with EngineStartOptions.ArtifactsDir");
            var counter = Interlocked.Increment(ref _artifactCounter);
            var name = counter.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)
                + (label is null ? "" : "-" + WebSupport.SanitizeFilename(label))
                + extension;
            var relative = folder + "/" + name;
            var absolute = Path.Combine(root, folder, name);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            return (relative, absolute);
        }

        public Task<BrowserDownload> WaitForDownloadAsync(Func<Task> trigger, TimeSpan timeout, CancellationToken cancellationToken) => RunAsync("download", cancellationToken, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var budget = Budget("download", cancellationToken, timeout);
            Exception? triggerFailure = null;
            (TimeSpan Wait, TimeSpan Trigger)? awaiting = null;
            try
            {
                var remote = _lease?.Downloads();
                if (remote is not null)
                {
                    await budget.WithinAsync(Downloads.SaveDownloadsToAsync(Page, remote.Dir)).ConfigureAwait(false);
                }

                var waitFor = TimeSpan.FromMilliseconds(budget.PlaywrightTimeout);
                var download = await budget.WithinAsync(Page.RunAndWaitForDownloadAsync(
                    async () =>
                    {
                        var started = _seams.Clock.GetTimestamp();
                        try
                        {
                            await trigger().ConfigureAwait(false);
                        }
                        catch (Exception cause)
                        {
                            triggerFailure = cause;
                            throw;
                        }

                        awaiting = (waitFor, _seams.Clock.GetElapsedTime(started));
                    },
                    new PageRunAndWaitForDownloadOptions { Timeout = budget.PlaywrightTimeout })).ConfigureAwait(false);
                awaiting = null;
                var suggested = download.SuggestedFilename;
                var (relative, absolute) = ArtifactPath("downloads", suggested, "");
                await budget.WithinAsync(remote is null
                    ? Downloads.SaveLocallyAsync(download, absolute, _lease?.UnservedDownloads())
                    : Downloads.SaveFromBrowserAsync(download, remote, absolute, cancellationToken)).ConfigureAwait(false);
                return new BrowserDownload { Path = relative, AbsolutePath = absolute, SuggestedFilename = suggested };
            }
            catch (Exception cause) when (triggerFailure is not null && ReferenceEquals(cause, triggerFailure))
            {
                // The trigger is test code, so its errors keep their original classification.
                throw;
            }
            catch (Exception cause) when (WebErrors.IsPlaywright(cause) || cause is EngineException { Code: EngineErrorCodes.OperationTimeout })
            {
                var translated = WebErrors.Translate(cause, "download");

                // The waiter's clock starts before the trigger runs, so the message names both.
                if (awaiting is { } waited && translated.Code == EngineErrorCodes.OperationTimeout)
                {
                    throw new TestException(
                        "ACTION_FAILED",
                        $"no download started within {(long)waited.Wait.TotalMilliseconds}ms; the trigger resolved after {(long)waited.Trigger.TotalMilliseconds}ms",
                        cause);
                }

                throw translated;
            }
        });

        public Task<IReadOnlyList<BrowserCookie>> GetCookiesAsync(CancellationToken cancellationToken) => RunAsync<IReadOnlyList<BrowserCookie>>("cookies", cancellationToken, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cookies = await RequireContext().CookiesAsync().ConfigureAwait(false);
            return cookies.Select(cookie => new BrowserCookie
            {
                Name = cookie.Name,
                Value = cookie.Value,
                Domain = cookie.Domain,
                Path = cookie.Path,
                Expires = cookie.Expires >= 0 ? (long)Math.Floor(cookie.Expires) : null,
                HttpOnly = cookie.HttpOnly,
                Secure = cookie.Secure,
                SameSite = cookie.SameSite.ToString(),
            }).ToList();
        });

        public Task SetCookiesAsync(IReadOnlyList<BrowserCookie> cookies, CancellationToken cancellationToken) => RunAsync("cookies", cancellationToken, () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(cookies);
            return RequireContext().AddCookiesAsync(cookies.Select(cookie => new Cookie
            {
                Name = cookie.Name,
                Value = cookie.Value,
                Url = cookie.Url,
                Domain = cookie.Url is null ? cookie.Domain : null,
                Path = cookie.Url is null ? cookie.Path ?? "/" : null,
                Expires = cookie.Expires,
                HttpOnly = cookie.HttpOnly,
                Secure = cookie.Secure,
                SameSite = cookie.SameSite switch
                {
                    null => null,
                    "Strict" => SameSiteAttribute.Strict,
                    "Lax" => SameSiteAttribute.Lax,
                    _ => SameSiteAttribute.None,
                },
            }));
        });

        public Task ClearCookiesAsync(CookieFilter? options, CancellationToken cancellationToken) => RunAsync("clearCookies", cancellationToken, () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return RequireContext().ClearCookiesAsync(options is null ? null : new BrowserContextClearCookiesOptions
            {
                Name = options.Name?.Text,
                NameRegex = options.Name?.Pattern,
                Domain = options.Domain?.Text,
                DomainRegex = options.Domain?.Pattern,
                Path = options.Path?.Text,
                PathRegex = options.Path?.Pattern,
            });
        });

        public Task SetViewportAsync(int width, int height, CancellationToken cancellationToken) => RunAsync("setViewport", cancellationToken, async () =>
        {
            if (width < 0 || height < 0)
            {
                throw new ArgumentOutOfRangeException(width < 0 ? nameof(width) : nameof(height), "the viewport needs whole, non-negative pixels, got " + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "x" + height.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            var budget = Budget("setViewport", cancellationToken);
            _viewport = new ViewportSize { Width = width, Height = height };
            if (_page is not null)
            {
                await budget.WithinAsync(_page.SetViewportSizeAsync(width, height)).ConfigureAwait(false);
            }
        });

        public Task EmulateMediaAsync(WebMedia media, CancellationToken cancellationToken) => RunAsync("emulateMedia", cancellationToken, async () =>
        {
            ArgumentNullException.ThrowIfNull(media);
            var context = RequireContext();
            var budget = Budget("emulateMedia", cancellationToken);

            // Merged before the first await, so a second call made meanwhile builds on this one instead of the old value.
            var merged = _media = _media.Merge(media);
            try
            {
                if (_page is { IsClosed: false } page)
                {
                    await budget.WithinAsync(page.EmulateMediaAsync(merged.ToPageOptions())).ConfigureAwait(false);
                }

                await EmulateOnOthersAsync(context, _page, other => budget.WithinAsync(other.EmulateMediaAsync(merged.ToPageOptions()))).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.Translate(ex, "emulateMedia");
            }
        });

        /// <summary>
        /// Runs <paramref name="emulate"/> on every open tab but the attempt's own. A tab the app closes
        /// meanwhile fails its own call only; an error on a tab still open propagates.
        /// </summary>
        private static Task EmulateOnOthersAsync(IBrowserContext context, IPage? own, Func<IPage, Task> emulate)
        {
            return Task.WhenAll(context.Pages.Where(page => page != own && !page.IsClosed).Select(async page =>
            {
                try
                {
                    await emulate(page).ConfigureAwait(false);
                }
                catch (Exception) when (page.IsClosed)
                {
                }
            }));
        }

        // Sets the attempt's media features on a page before it shows anything; a page with none set keeps the browser's own.
        private Task ApplyMediaAsync(IPage page) => _media.IsEmpty ? Task.CompletedTask : page.EmulateMediaAsync(_media.ToPageOptions());

        // Emulates the attempt's media on each tab as it appears, so a tab the app opens itself follows a test's change
        // too, on a persistent context as well, where the engine sets no context options. Playwright emulates per page, so
        // a test's change reaches it once it exists: its scripts may read the context's configured values until then. A tab the
        // attempt opens awaits the same work in NewPageAsync; a tab the app opens is not awaited.
        private void WatchPages(IBrowserContext context)
        {
            context.Page += (_, page) =>
            {
                var applied = ApplyMediaAsync(page);
                _emulated.AddOrUpdate(page, applied);
                _ = applied.ContinueWith(static task => task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            };
        }

        public Task AddInitScriptAsync(string source, CancellationToken cancellationToken) => RunAsync("addInitScript", cancellationToken, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = RequireContext();
            await context.AddInitScriptAsync(source).ConfigureAwait(false);
            _initScripts.Add(source);
        });

        public Task KeyboardTypeAsync(string text, CancellationToken cancellationToken) => RunAsync("keyboard.type", cancellationToken, () =>
            InputAsync(() => Page.Keyboard.TypeAsync(text), "keyboard.type", cancellationToken));

        public Task KeyboardDownAsync(string key, CancellationToken cancellationToken) => RunAsync("keyboard.down", cancellationToken, () =>
            InputAsync(() => Page.Keyboard.DownAsync(key), "keyboard.down " + key, cancellationToken));

        public Task KeyboardUpAsync(string key, CancellationToken cancellationToken) => RunAsync("keyboard.up", cancellationToken, () =>
            InputAsync(() => Page.Keyboard.UpAsync(key), "keyboard.up " + key, cancellationToken));

        public Task MouseMoveAsync(float x, float y, CancellationToken cancellationToken) => RunAsync("mouse.move", cancellationToken, () =>
            InputAsync(() => Page.Mouse.MoveAsync(x, y), "mouse.move", cancellationToken));

        public Task MouseWheelAsync(float deltaX, float deltaY, CancellationToken cancellationToken) => RunAsync("mouse.wheel", cancellationToken, () =>
            InputAsync(() => Page.Mouse.WheelAsync(deltaX, deltaY), "mouse.wheel", cancellationToken));

        public Task MouseDownAsync(CancellationToken cancellationToken) => RunAsync("mouse.down", cancellationToken, () =>
            InputAsync(() => Page.Mouse.DownAsync(), "mouse.down", cancellationToken));

        public Task MouseUpAsync(CancellationToken cancellationToken) => RunAsync("mouse.up", cancellationToken, () =>
            InputAsync(() => Page.Mouse.UpAsync(), "mouse.up", cancellationToken));

        /// <summary>Opens a clean context at the current viewport. Its first page opens on the first navigation.</summary>
        public async Task NewContextAsync()
        {
            var context = await _browser.NewContextAsync(ContextOptions(_options, _viewport, _credentials)).ConfigureAwait(false);
            await ConfigureAsync(context).ConfigureAwait(false);
            _context = context;
        }

        public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => SwipeAsync(direction, null, cancellationToken);

        public Task SwipeAsync(ScrollDirection direction, Momentum? momentum, CancellationToken cancellationToken) => ActedAsync(cancellationToken, () => RunAsync("scroll", cancellationToken, async () =>
        {
            RequireObservation();
            var budget = Budget("scroll", cancellationToken);
            try
            {
                await budget.WithinAsync(Page.EvaluateAsync(PageScript.ScrollViewport, new object[] { Direction(direction), Stride(momentum) })).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.NavigationStaleOr(ex, "scroll");
            }
            catch (EngineException ex) when (ex.Code == EngineErrorCodes.OperationTimeout)
            {
                // As upstream's viewport swipe, a scroll the deadline cut off may have moved the page.
                throw WebErrors.ClassifyAction(ex, new LocatorAction.Swipe(direction, momentum));
            }
        }));

        public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) => RunAsync("screenshot", cancellationToken, async () =>
        {
            var budget = Budget("screenshot", cancellationToken);
            try
            {
                var page = Page;
                var masks = SecureFieldMasks(page);
                var masked = await CountMasksAsync(masks, budget).ConfigureAwait(false);

                // The adopted stylesheet hides the caret where Playwright's own option would write an inline style a hydrating framework flags.
                var png = await ScreenshotCaret.WithHiddenCaretAsync(page, () => budget.WithinAsync(page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Type = ScreenshotType.Png,
                    Animations = ScreenshotAnimations.Disabled,
                    Caret = Microsoft.Playwright.ScreenshotCaret.Initial,
                    Mask = masks,
                    MaskColor = "#000000",
                    Timeout = budget.PlaywrightTimeout,
                }))).ConfigureAwait(false);
                var scale = await budget.WithinAsync(page.EvaluateAsync<double>("() => window.devicePixelRatio")).ConfigureAwait(false);
                return new EngineScreenshot(png, scale > 0 ? scale : 1, masked);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.NavigationStaleOr(ex, "screenshot");
            }
        });

        // Every secure field of every frame is covered before the image leaves the engine: two locators per
        // frame, one through light DOM and open shadow roots, one through the closed roots.
        private static List<ILocator> SecureFieldMasks(IPage page) =>
            page.Frames.SelectMany(frame => new[] { frame.Locator(SecureFieldSelector), frame.Locator(ClosedSecureFieldSelector) }).ToList();

        // How many regions the masks cover, which a caller proves the image is at least as redacted as the tree with.
        private static async Task<int> CountMasksAsync(List<ILocator> masks, OperationBudget budget)
        {
            var masked = 0;
            foreach (var mask in masks)
            {
                try
                {
                    masked += await budget.WithinAsync(mask.CountAsync()).ConfigureAwait(false);
                }
                catch (Exception ex) when (WebErrors.IsPlaywright(ex))
                {
                    // A frame that detaches mid-sweep contributes nothing, which can only push a caller toward withholding the image.
                }
            }

            return masked;
        }

        /// <summary>
        /// A redacted screenshot artifact: secure fields in every frame are masked at the source, as in observation pixels. The file is
        /// written through a unique temporary name and moved into place (upstream's <c>screenshot</c> in <c>web/src/surface.ts</c>).
        /// </summary>
        public Task<string> ScreenshotArtifactAsync(string? label, CancellationToken cancellationToken) => RunAsync("screenshot", cancellationToken, async () =>
        {
            var budget = Budget("screenshot", cancellationToken);
            try
            {
                var page = Page;
                var (relative, absolute) = ArtifactPath("screenshots", label, ".png");
                // As upstream's artifact capture, the locators go straight to the screenshot call: no count to spend the budget on.
                var masks = SecureFieldMasks(page);
                var png = await budget.WithinAsync(page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Type = ScreenshotType.Png,
                    Mask = masks,
                    MaskColor = "#000000",
                    Timeout = budget.PlaywrightTimeout,
                })).ConfigureAwait(false);
                var temporary = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await File.WriteAllBytesAsync(temporary, png, cancellationToken).ConfigureAwait(false);
                    File.Move(temporary, absolute, overwrite: true);
                }
                finally
                {
                    File.Delete(temporary);
                }

                return relative;
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.NavigationStaleOr(ex, "screenshot");
            }
        });

        /// <summary>
        /// Waits for a route or dialog handler still running, then rethrows the error one of them latched after the last step, so it fails
        /// this attempt instead of the next one's first operation.
        /// </summary>
        public Task SettleAttemptAsync(TimeSpan timeout, CancellationToken cancellationToken) => _latch.SettleAsync(timeout, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            // Late recovery and page work can only reach this retired session.
            _disposed = true;
            var connected = _lease is not null && _browser.IsConnected;
            await _lifetime.CancelAsync().ConfigureAwait(false);
            try
            {
                if (_recovery is { } recovery)
                {
                    await recovery.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                }

                await AbandonVideoAsync().ConfigureAwait(false);

                // An ordinary context is engine-owned; a persistent browser belongs to the host, so only the connection closes.
                if (_context is not null && _persistent is null)
                {
                    await CloseDroppedAsync(() => _context.CloseAsync()).ConfigureAwait(false);
                }
            }
            finally
            {
                try
                {
                    await CloseDroppedAsync(() => _browser.CloseAsync()).ConfigureAwait(false);
                }
                finally
                {
                    _playwright.Dispose();
                    _lifetime.Dispose();

                    // A leased browser is released after its connection closes, whether or not the close succeeded.
                    if (_lease is not null)
                    {
                        await _lease.CloseAsync(connected).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Runs a close that finds its target already gone: a grid that dropped the session before the connection noticed answers the close with
        /// a closed-target error, and a browser that is gone is as closed as the close would leave it. .NET-only (upstream has no Playwright-protocol lease).
        /// </summary>
        private static async Task CloseDroppedAsync(Func<Task> close)
        {
            try
            {
                await close().ConfigureAwait(false);
            }
            // Playwright for .NET keeps TargetClosedException internal, so it is told by name.
            catch (PlaywrightException ex) when (ex.GetType().Name == "TargetClosedException")
            {
            }
        }

        /// <summary>
        /// Reads one frame's document, then each child frame's into the iframe
        /// node that owns it, sharing one node budget across all of them. A
        /// frame that detaches or navigates mid-read is left empty.
        /// </summary>
        private async Task<List<WebNode>> CollectAsync(IFrame frame, FrameWalk walk, OperationBudget budget)
        {
            if (walk.Remaining <= 0)
            {
                walk.Truncated = true;
                return [];
            }

            // A read the budget abandons keeps minting refs in the page. The trace's read reserves the most it can mint up front,
            // so the next read never starts from a number the abandoned one is still handing out.
            var seed = _nextRef;
            if (!walk.Publish)
            {
                _nextRef = seed + walk.Remaining;
            }

            System.Text.Json.JsonElement json;
            try
            {
                json = await budget.WithinAsync(frame.EvaluateAsync<System.Text.Json.JsonElement>(PageScript.Collect, new { seed, max = walk.Remaining, testIdAttribute = _testIdAttribute, publish = walk.Publish })).ConfigureAwait(false);
            }
            catch (PlaywrightException) when (frame != _page?.MainFrame)
            {
                return [];
            }

            var dto = System.Text.Json.JsonSerializer.Deserialize<WebObservation>(json, JsonDefaults.Options);
            if (dto is null)
            {
                return [];
            }

            _nextRef = Math.Max(_nextRef, dto.Next);
            walk.Remaining -= dto.Count;
            walk.Truncated |= dto.Truncated;
            if (frame.ParentFrame is null)
            {
                walk.Scroll = dto.Scroll;
                walk.Viewport = dto.Viewport is { Width: > 0, Height: > 0 } size ? new TraceViewport(size.Width, size.Height) : null;
            }

            var roots = dto.Roots ?? [];
            var owners = new Dictionary<string, WebNode>(StringComparer.Ordinal);
            Index(roots, frame, walk, owners);
            foreach (var child in frame.ChildFrames.ToArray())
            {
                var (owner, inset) = await budget.WithinAsync(OwnerOfAsync(child)).ConfigureAwait(false);
                if (owner is not null && owners.TryGetValue(owner, out var node))
                {
                    // The child measured its nodes against its own viewport. Every box in the tree is in the top-level viewport's CSS pixels.
                    node.Children = PlaceInFrame(await CollectAsync(child, walk, budget).ConfigureAwait(false), node.Rect, inset);
                }
            }

            return roots;
        }

        /// <summary>
        /// Whether upstream's projection leaves the terminal query's predicate to the surface (<c>ProjectedLocator.displayValue</c> and
        /// <c>name</c>): a display-value query, or an exact label query. Its matches are pinned to the elements the read returned.
        /// </summary>
        private static bool PinsToElements(LocatorExpression expression)
        {
            while (expression.Kind is "filter" or "index" or "frame")
            {
                expression = expression.Source!;
            }

            return expression.Kind == "displayValue" || (expression.Kind == "label" && expression.Value?.Text is not null && expression.Exact);
        }

        /// <summary>
        /// A node located as the only match of its locator keeps that locator strict, as upstream's does. When a second element now matches
        /// it, the node is ambiguous: acting could hit the wrong element, so it is stale. A node the observation listed, or one of several
        /// matches, names its own element and is never ambiguous.
        /// </summary>
        private async Task RequireNoNewTwinAsync(SemanticNode node, OperationBudget budget)
        {
            if (!_strict.TryGetValue(node, out var expression))
            {
                return;
            }

            try
            {
                var roots = await CollectAsync(Page.MainFrame, new FrameWalk(), budget).ConfigureAwait(false);
                var projected = WebLocators.NeedsPage(expression) ? await ProjectAsync(expression, budget).ConfigureAwait(false) : expression;
                var observation = new Observation { Route = Route, Roots = roots.Select(each => ToNode(each, _testIdAttribute)).ToList() };
                if (LocatorResolver.Resolve(observation, projected, expression.IncludeHidden).Count > 1)
                {
                    throw new EngineException(EngineErrorCodes.NodeStale, $"Node {node.Ref} now matches more than one element. Observe again.", retryable: true);
                }
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.NavigationStaleOr(ex, "locate " + node.Ref);
            }
        }

        /// <summary>One operation's budget: the action timeout, shared by every page call in the operation.</summary>
        private OperationBudget Budget(string label, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
            new(timeout ?? _actionTimeout, label, cancellationToken, _seams.Clock, _operationStart.Value);

        /// <summary>Sends one keystroke or pointer event, with no element behind it, within its budget, and classifies its failure.</summary>
        private async Task InputAsync(Func<Task> input, string label, CancellationToken cancellationToken)
        {
            var budget = Budget(label, cancellationToken);
            try
            {
                await budget.WithinAsync(input()).ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex) || ex is EngineException { Code: EngineErrorCodes.OperationTimeout })
            {
                throw WebErrors.ClassifyInput(ex, label);
            }
        }

        /// <summary>Sends one action to the element, each Playwright call within what is left of the operation's budget.</summary>
        private async Task DispatchAsync(IElementHandle element, LocatorAction action, OperationBudget budget)
        {
            switch (action)
            {
                case LocatorAction.Tap tap:
                    await element.ClickAsync(new ElementHandleClickOptions { Timeout = budget.PlaywrightTimeout, Modifiers = Held(tap.Modifiers) }).ConfigureAwait(false);
                    break;
                case LocatorAction.DoubleTap doubleTap:
                    await element.DblClickAsync(new ElementHandleDblClickOptions { Timeout = budget.PlaywrightTimeout, Modifiers = Held(doubleTap.Modifiers) }).ConfigureAwait(false);
                    break;
                case LocatorAction.Fill fill:
                    await element.FillAsync(fill.Value, new ElementHandleFillOptions { Timeout = budget.PlaywrightTimeout }).ConfigureAwait(false);
                    break;
                case LocatorAction.Press press:
                    await element.PressAsync(press.Key, new ElementHandlePressOptions { Timeout = budget.PlaywrightTimeout }).ConfigureAwait(false);
                    break;
                case LocatorAction.PressSequentially typed:
                    await element.FocusAsync().ConfigureAwait(false);

                    // A focus the deadline cut off must not type later, into whatever holds focus then.
                    budget.ThrowIfExpired();
                    await Page.Keyboard.TypeAsync(typed.Text, new KeyboardTypeOptions
                    {
                        Delay = typed.Delay is TimeSpan delay ? (float)delay.TotalMilliseconds : null,
                    }).ConfigureAwait(false);
                    break;
                case LocatorAction.Select select:
                    await element.SelectOptionAsync(select.Value, new ElementHandleSelectOptionOptions { Timeout = budget.PlaywrightTimeout }).ConfigureAwait(false);
                    break;
                case LocatorAction.Check:
                    await SetCheckedAsync(element, true, budget).ConfigureAwait(false);
                    break;
                case LocatorAction.Uncheck:
                    await SetCheckedAsync(element, false, budget).ConfigureAwait(false);
                    break;
                case LocatorAction.Clear:
                    await element.FillAsync("", new ElementHandleFillOptions { Timeout = budget.PlaywrightTimeout }).ConfigureAwait(false);
                    break;
                case LocatorAction.Focus:
                    await element.FocusAsync().ConfigureAwait(false);
                    break;
                case LocatorAction.ScrollIntoView:
                    await element.ScrollIntoViewIfNeededAsync(new ElementHandleScrollIntoViewIfNeededOptions { Timeout = budget.PlaywrightTimeout }).ConfigureAwait(false);
                    break;
                case LocatorAction.Swipe swipe:
                    await element.EvaluateAsync(PageScript.ScrollElement, new object[] { Direction(swipe.Direction), Stride(swipe.Momentum) }).ConfigureAwait(false);
                    break;
                default:
                    throw new EngineException(EngineErrorCodes.UnsupportedCapability, "Web engine cannot perform " + action.GetType().Name + ".");
            }
        }

        private static async Task<(string? Ref, WebRect? Inset)> OwnerOfAsync(IFrame frame)
        {
            try
            {
                var element = await frame.FrameElementAsync().ConfigureAwait(false);
                await using var dispose = element.ConfigureAwait(false);
                var owner = await element.EvaluateAsync<string?>(PageScript.RefOf).ConfigureAwait(false);
                return (owner, await element.EvaluateAsync<WebRect?>(PageScript.FrameInset).ConfigureAwait(false));
            }
            catch (PlaywrightException)
            {
                return (null, null);
            }
        }

        /// <summary>
        /// Moves a child document's boxes into the top-level viewport's space: shifted by the frame element's content box
        /// and clipped to it, so a node that overflows its frame claims no point of the page around it. A box left empty
        /// by the clip, or inside a frame with no box, is dropped. A frame under a CSS transform is not unwound.
        /// </summary>
        private static List<WebNode> PlaceInFrame(List<WebNode> nodes, WebRect? border, WebRect? inset)
        {
            WebRect? content = border is null || inset is null
                ? null
                : new WebRect { X = border.X + inset.X, Y = border.Y + inset.Y, Width = inset.Width, Height = inset.Height };
            Place(nodes, content);
            return nodes;
        }

        private static void Place(List<WebNode>? nodes, WebRect? content)
        {
            foreach (var node in nodes ?? [])
            {
                node.Rect = content is null || node.Rect is null ? null : Clip(node.Rect, content);
                Place(node.Children, content);
                Place(node.Inline, content);
            }
        }

        private static WebRect? Clip(WebRect rect, WebRect frame)
        {
            var x0 = Math.Max(rect.X + frame.X, frame.X);
            var y0 = Math.Max(rect.Y + frame.Y, frame.Y);
            var x1 = Math.Min(rect.X + frame.X + rect.Width, frame.X + frame.Width);
            var y1 = Math.Min(rect.Y + frame.Y + rect.Height, frame.Y + frame.Height);
            return x1 > x0 && y1 > y0 ? new WebRect { X = x0, Y = y0, Width = x1 - x0, Height = y1 - y0 } : null;
        }

        private static void Index(List<WebNode> nodes, IFrame frame, FrameWalk walk, Dictionary<string, WebNode> owners)
        {
            foreach (var node in nodes)
            {
                walk.Frames[node.Ref] = frame;
                if (node.Frame)
                {
                    owners[node.Ref] = node;
                }

                Index(node.Children ?? [], frame, walk, owners);
                Index(node.Inline ?? [], frame, walk, owners);
            }
        }

        /// <summary>Opens the active page once; concurrent callers share it.</summary>
        private Task<IPage> EnsurePageAsync()
        {
            lock (_pageLock)
            {
                if (_page is { IsClosed: false } open)
                {
                    return Task.FromResult(open);
                }

                return _pendingPage is { IsCompleted: false } pending ? pending : _pendingPage = NewPageAsync(RequireContext());
            }
        }

        private async Task<IPage> NewPageAsync(IBrowserContext context)
        {
            var page = await NextPageAsync(context).ConfigureAwait(false);
            page.SetDefaultTimeout(ActionMs);
            if (_viewport is { } viewport)
            {
                await page.SetViewportSizeAsync(viewport.Width, viewport.Height).ConfigureAwait(false);
            }

            await (_emulated.TryGetValue(page, out var emulated) ? emulated : ApplyMediaAsync(page)).ConfigureAwait(false);

            if (_persistent is not null)
            {
                var target = await TargetIdentityAsync(page).ConfigureAwait(false);

                // An identity that arrives after the attempt ended belongs to no page of the next one.
                if (_disposed)
                {
                    throw new EngineException(EngineErrorCodes.NodeStale, "the connection changed while reading the screen", retryable: true);
                }

                _target = target;
            }

            try
            {
                await VideoPageOpenedAsync(page).ConfigureAwait(false);
            }
            catch
            {
                await page.CloseAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                throw;
            }

            _page = page;
            return page;
        }

        /// <summary>
        /// The page the attempt shows next. A persistent browser is fresh for the
        /// attempt, so its own first tab serves as the attempt's first page instead
        /// of a second tab beside it, which a hosted browser's live view would show
        /// behind the test's. The tab is navigated to <c>about:blank</c> first, so its
        /// document runs the context's init scripts as a new tab's does. Every later
        /// page is a new tab.
        /// </summary>
        private async Task<IPage> NextPageAsync(IBrowserContext context)
        {
            if (_firstPage && _persistent is not null && context.Pages.FirstOrDefault(page => !page.IsClosed) is { } initial)
            {
                // A navigation that failed leaves the tab to the next attempt at opening the first page.
                await initial.GotoAsync("about:blank", new PageGotoOptions { Timeout = ActionMs }).ConfigureAwait(false);
                _firstPage = false;
                return initial;
            }

            _firstPage = false;
            return await context.NewPageAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Installs what every context of the attempt carries: the closed-root hook, the
        /// init scripts configured then added, the site headers, and the test's routes.
        /// </summary>
        private async Task ConfigureAsync(IBrowserContext context)
        {
            await context.AddInitScriptAsync(PageScript.RecordClosedShadowRoots).ConfigureAwait(false);
            foreach (var script in _initScripts)
            {
                await context.AddInitScriptAsync(script).ConfigureAwait(false);
            }

            await _setUpContext(context).ConfigureAwait(false);

            // Every dialog of every page, the app's own tabs included, goes to the router: one nobody handles fails the next step.
            context.Dialog += (_, dialog) => _ = _dialogs.DispatchAsync(dialog);
            AppLog.Install(
                context,
                entry => _appLog?.Invoke(entry),
                new NavigationLog(line => _navigation?.Invoke(line), _ => Volatile.Read(ref _ownNavigations) > 0, OpenPage));
            foreach (var (matches, playwrightHandler, _) in _routes)
            {
                await context.RouteAsync(matches, playwrightHandler).ConfigureAwait(false);
            }

            WatchPages(context);
        }

        /// <summary>
        /// Opens an attempt on a fresh remote browser provisioned by <c>cdpEndpoint</c>,
        /// riding its default context. A browser an earlier attempt rode is refused.
        /// The first page opens on the first navigation.
        /// </summary>
        public static async Task<WebSession> StartPersistentAsync(
            IPlaywright playwright,
            PersistentConnect persistent,
            TimeSpan actionTimeout,
            WebEngineOptions options,
            List<string> initScripts,
            Func<IBrowserContext, Task> setUpContext,
            Func<string, IReadOnlyDictionary<string, string>?> siteHeaders,
            SessionSeams seams,
            LeaseScope? lease,
            VideoSetup video,
            CancellationToken cancellationToken)
        {
            SessionBinding binding;
            try
            {
                await RegisterSelectorEnginesAsync(playwright).ConfigureAwait(false);

                // As upstream, attaching a persistent attempt has the browser launch budget.
                binding = await AttachWithinAsync(playwright, seams, persistent.Provision, E2EDefaults.LaunchTimeout, seams.Clock.GetTimestamp(), null, "connection", cancellationToken, CancellationToken.None, static _ => Task.CompletedTask).ConfigureAwait(false);
            }
            catch
            {
                playwright.Dispose();
                throw;
            }

            var session = new WebSession(playwright, binding.Browser, actionTimeout, options, initScripts, setUpContext, siteHeaders, seams, persistent, lease: lease) { Video = video, ArtifactsDir = video.ArtifactsDir };
            try
            {
                if (!persistent.Claim(binding.ContextId))
                {
                    throw RecoveryFailed("cdpEndpoint reused a browser from a previous attempt; provision a fresh browser");
                }

                session._context = binding.Context;
                session._contextId = binding.ContextId;
                await session.ConfigureAsync(binding.Context).ConfigureAwait(false);
                return session;
            }
            catch
            {
                await session.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>Observation-backed input needs evidence captured after a reconnect.</summary>
        private void RequireObservation()
        {
            if (!_observed)
            {
                throw new EngineException(EngineErrorCodes.NodeStale, "observe the screen again after CDP recovery before acting", retryable: true);
            }
        }

        private Task RunAsync(string label, CancellationToken cancellationToken, Func<Task> work) =>
            _persistent is null
                ? work()
                : RunPersistentAsync(label, cancellationToken, async () =>
                {
                    await work().ConfigureAwait(false);
                    return true;
                });

        /// <summary>
        /// Runs one operation. On a persistent context whose transport dropped, it
        /// first shares one reconnect, then dispatches once with the time that remains.
        /// An operation is never retried.
        /// </summary>
        private Task<T> RunAsync<T>(string label, CancellationToken cancellationToken, Func<Task<T>> work)
        {
            return _persistent is null ? work() : RunPersistentAsync(label, cancellationToken, work);
        }

        private async Task<T> RunPersistentAsync<T>(string label, CancellationToken cancellationToken, Func<Task<T>> work)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_failure is not null)
            {
                throw _failure;
            }

            if (_recovery is null && _browser.IsConnected)
            {
                return await work().ConfigureAwait(false);
            }

            var started = _seams.Clock.GetTimestamp();
            Task recovery;
            lock (_recoveryLock)
            {
                // Started on the pool, so the resolver, which is user code, never runs under the lock.
                recovery = _recovery ??= Task.Run(() => ReconnectAsync(label, started, cancellationToken), CancellationToken.None);
            }

            try
            {
                await recovery.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && _failure is not null)
            {
                // The reconnect this operation waited on was cancelled by the operation that started it.
                throw _failure;
            }

            lock (_recoveryLock)
            {
                if (_recovery == recovery)
                {
                    _recovery = null;
                }
            }

            // Recovery is the engine's own work, so it counts against the operation's budget.
            _operationStart.Value = started;
            return await work().ConfigureAwait(false);
        }

        /// <summary>
        /// Reconnects through <c>reconnectEndpoint</c> to the same browser and page,
        /// within the budget of the operation that found the transport dropped. Every
        /// observed ref is retired. A failure ends the attempt: a failed reconnect never
        /// quietly provisions another session.
        /// </summary>
        private async Task ReconnectAsync(string label, long started, CancellationToken cancellationToken)
        {
            var previous = (_contextId!, _target);
            var size = _page?.ViewportSize is { } current ? new ViewportSize { Width = current.Width, Height = current.Height } : _viewport;
            _observed = false;
            _frames = new(StringComparer.Ordinal);
            _strict.Clear();
            try
            {
                await VideoPageClosingAsync().ConfigureAwait(false);
                var binding = await AttachWithinAsync(_playwright, _seams, _persistent!.Reconnect, _actionTimeout, started, previous, label, cancellationToken, _lifetime.Token, async candidate =>
                {
                    await ConfigureAsync(candidate.Context).ConfigureAwait(false);
                    if (candidate.Page is { } page)
                    {
                        page.SetDefaultTimeout(ActionMs);
                        if (size is { } viewport)
                        {
                            await page.SetViewportSizeAsync(viewport.Width, viewport.Height).ConfigureAwait(false);
                        }

                        // Emulation belongs to the CDP session that set it, so the new transport sets it again, on the app's own tabs too.
                        await ApplyMediaAsync(page).ConfigureAwait(false);
                        await EmulateOnOthersAsync(candidate.Context, page, ApplyMediaAsync).ConfigureAwait(false);
                        await VideoPageOpenedAsync(page).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
                _browser = binding.Browser;
                _context = binding.Context;
                _page = binding.Page;
                _target = binding.Target;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _lifetime.IsCancellationRequested)
            {
                _failure = new EngineException(EngineErrorCodes.Cancelled, label + " cancelled", retryable: false);
                throw;
            }
            catch (Exception ex)
            {
                _failure = ex is EngineException ? ex : WebErrors.IsPlaywright(ex) ? WebErrors.Translate(ex, "connection") : ex;
                throw _failure;
            }
        }

        /// <summary>
        /// Attaches, then runs <paramref name="prepare"/> on the candidate, all within what is
        /// left of <paramref name="budget"/> since <paramref name="started"/>: a resolver, dial,
        /// or configuration still pending at the deadline is cancelled, and the operation fails
        /// with <c>OPERATION_TIMEOUT</c>. A candidate that fails or arrives late is closed.
        /// </summary>
        private static async Task<SessionBinding> AttachWithinAsync(
            IPlaywright playwright,
            SessionSeams seams,
            Func<CancellationToken, Task<string>> resolve,
            TimeSpan budget,
            long started,
            (string ContextId, TargetIdentity? Target)? previous,
            string label,
            CancellationToken cancellationToken,
            CancellationToken lifetime,
            Func<SessionBinding, Task> prepare)
        {
            TimeSpan Remaining() => budget - seams.Clock.GetElapsedTime(started);
            using var timer = new CancellationTokenSource(Remaining() > TimeSpan.Zero ? Remaining() : TimeSpan.Zero, seams.Clock);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime, timer.Token);
            var token = deadline.Token;
            async Task<SessionBinding> AttachAndPrepareAsync()
            {
                var candidate = await AttachPersistentAsync(playwright, seams, resolve, Remaining, previous, label, token).ConfigureAwait(false);
                try
                {
                    await prepare(candidate).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    return candidate;
                }
                catch
                {
                    await CloseQuietlyAsync(candidate.Browser).ConfigureAwait(false);
                    throw;
                }
            }

            var attach = AttachAndPrepareAsync();
            try
            {
                return await attach.WaitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // A connection that arrives once the attach was abandoned is closed; a failure has no one to reach.
                _ = attach.ContinueWith(
                    static abandoned => abandoned.IsCompletedSuccessfully ? CloseQuietlyAsync(abandoned.Result.Browser) : Task.FromResult(abandoned.Exception),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
                if (cancellationToken.IsCancellationRequested || lifetime.IsCancellationRequested)
                {
                    throw;
                }

                throw new EngineException(EngineErrorCodes.OperationTimeout, label + " timed out", retryable: false);
            }
        }

        /// <summary>
        /// Owns an uncommitted connection until its identity is proven; a cancelled or
        /// late connection is closed. With <paramref name="previous"/>, the browser must
        /// be the one the attempt rode and must still hold its exact page.
        /// </summary>
        private static async Task<SessionBinding> AttachPersistentAsync(
            IPlaywright playwright,
            SessionSeams seams,
            Func<CancellationToken, Task<string>> resolve,
            Func<TimeSpan> remaining,
            (string ContextId, TargetIdentity? Target)? previous,
            string label,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var endpoint = await resolve(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw RecoveryFailed("the endpoint is empty");
            }

            // The resolver's time is spent from the dial's budget, as from the operation's.
            var left = remaining();
            if (left <= TimeSpan.Zero)
            {
                throw new EngineException(EngineErrorCodes.OperationTimeout, label + " timed out", retryable: false);
            }

            var browser = await seams.ConnectCdp(playwright, endpoint, left, token).ConfigureAwait(false);

            // A connection whose owner gave up is detached at once, not when its next read returns.
            using var detach = token.Register(() => _ = CloseQuietlyAsync(browser));
            try
            {
                token.ThrowIfCancellationRequested();
                var session = await browser.NewBrowserCDPSessionAsync().ConfigureAwait(false);
                string? contextId;
                try
                {
                    var contexts = (await session.SendAsync("Target.getBrowserContexts").ConfigureAwait(false))!.Value;
                    if (contexts.GetProperty("browserContextIds").GetArrayLength() > 0)
                    {
                        throw RecoveryFailed("the remote browser must contain only its dedicated default context");
                    }

                    contextId = contexts.TryGetProperty("defaultBrowserContextId", out var id) ? id.GetString() : null;
                }
                finally
                {
                    await DetachQuietlyAsync(session).ConfigureAwait(false);
                }

                token.ThrowIfCancellationRequested();
                var context = browser.Contexts.FirstOrDefault() ?? throw RecoveryFailed("the default context is missing");
                if (string.IsNullOrEmpty(contextId))
                {
                    throw RecoveryFailed("the default context identity is unavailable");
                }

                if (previous is { } prior && !string.Equals(contextId, prior.ContextId, StringComparison.Ordinal))
                {
                    throw RecoveryFailed("reconnectEndpoint returned a different browser");
                }

                if (previous?.Target is not { } original)
                {
                    token.ThrowIfCancellationRequested();
                    return new SessionBinding(browser, context, null, contextId, null);
                }

                foreach (var page in context.Pages)
                {
                    var target = await TargetIdentityAsync(page).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (!string.Equals(target.TargetId, original.TargetId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!string.Equals(target.BrowserContextId, original.BrowserContextId, StringComparison.Ordinal))
                    {
                        throw RecoveryFailed("the original page belongs to a different context");
                    }

                    await RequireShadowTrackingAsync(page, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    return new SessionBinding(browser, context, page, contextId, target);
                }

                throw RecoveryFailed("the original page no longer exists");
            }
            catch
            {
                await CloseQuietlyAsync(browser).ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>Reads protocol identity, independent of URL, document contents, and tab order.</summary>
        private static async Task<TargetIdentity> TargetIdentityAsync(IPage page)
        {
            var session = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
            try
            {
                var info = (await session.SendAsync("Target.getTargetInfo").ConfigureAwait(false))!.Value.GetProperty("targetInfo");
                return new TargetIdentity(
                    info.GetProperty("targetId").GetString()!,
                    info.TryGetProperty("browserContextId", out var context) ? context.GetString() : null);
            }
            finally
            {
                await DetachQuietlyAsync(session).ConfigureAwait(false);
            }
        }

        /// <summary>A document that changed while disconnected never ran the closed-root hook, which observation needs.</summary>
        private static async Task RequireShadowTrackingAsync(IPage page, CancellationToken token)
        {
            foreach (var frame in page.Frames)
            {
                var tracked = await frame.EvaluateAsync<bool>(PageScript.TracksClosedShadowRoots).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (!tracked)
                {
                    throw new EngineException(
                        EngineErrorCodes.EngineFailure,
                        "CDP recovery failed: closed shadow root tracking is unavailable after a document changed while disconnected; locators and secure-field masks need it",
                        retryable: false);
                }
            }
        }

        /// <summary>A failed reconnect must never quietly provision another session.</summary>
        private static EngineException RecoveryFailed(string detail) =>
            new(EngineErrorCodes.EngineFailure, "CDP recovery failed: " + detail, retryable: false);

        private static async Task CloseQuietlyAsync(IBrowser browser)
        {
            try
            {
                await browser.CloseAsync().ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // The connection is already gone.
            }
        }

        private static async Task DetachQuietlyAsync(ICDPSession session)
        {
            try
            {
                await session.DetachAsync().ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // The target or connection is already gone.
            }
        }

        private IBrowserContext RequireContext()
        {
            ThrowPending();
            return _context ?? throw new EngineException(EngineErrorCodes.InvalidState, "The browser has no context.");
        }

        /// <summary>
        /// Rethrows, once, the first error a route or dialog handler raised since the last
        /// operation. Nothing awaits a handler, so its error fails the next
        /// operation that reaches the page or the context.
        /// </summary>
        private void ThrowPending() => _latch.ThrowPending();

        /// <summary>
        /// Runs one handler on one request. The error is kept before an undecided
        /// request is aborted, so the step after the page saw the failure reports it.
        /// </summary>
        private async Task HandleRouteAsync(IRoute route, Func<IBrowserRoute, Task> handler)
        {
            var decision = new PlaywrightRoute(route, _siteHeaders);
            try
            {
                await handler(decision).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _latch.Latch(ex);
                if (!decision.Decided)
                {
                    await PlaywrightRoute.AbortQuietlyAsync(route).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Reads the body of a known response. A body the browser could not read
        /// faults with <c>ACTION_FAILED</c> naming the cause, never an empty string
        /// the server did not send. The browser reports the reason on the request
        /// (<c>net::ERR_CONTENT_LENGTH_MISMATCH</c> for a connection cut short of the
        /// declared length), and the error behind the read is the fallback, as for
        /// a redirect, whose body the browser never keeps.
        /// </summary>
        private static async Task<string> ReadBodyAsync(IResponse response)
        {
            try
            {
                return System.Text.Encoding.UTF8.GetString(await response.BodyAsync().ConfigureAwait(false));
            }
            catch (PlaywrightException ex)
            {
                var reason = response.Request.Failure ?? WebErrors.Message(ex);
                throw new TestException("ACTION_FAILED", "waitForResponse: response body could not be read: " + reason, ex);
            }
        }

        /// <summary>How often a check or uncheck reads the control's state after the click.</summary>
        private static readonly TimeSpan CheckPollInterval = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Sets a checkbox, switch, or radio to <paramref name="checked"/> with one click, as
        /// Playwright's <c>check</c> does, apart from the reads after the click. Playwright
        /// reads the state once; here it is polled until just before the operation's deadline, so a
        /// control that commits its new state after an await or a timer passes, and one that
        /// never changes fails at the deadline. A control that is gone by the first read (an
        /// app that swaps a picked radio for its selected view, or navigates on change) took
        /// the click, so the action is done, where Playwright reports it detached as if the
        /// click never happened. One that goes later, still in its old state, may have been
        /// replaced for any reason, so it fails rather than passing on a click the app
        /// rejected. Whatever took its place is not read: the next observation or assertion
        /// shows it, as it does after a tap. The reads and the click share one element, so
        /// the state before and after the click is one control's.
        /// </summary>
        private static async Task SetCheckedAsync(IElementHandle element, bool @checked, OperationBudget budget)
        {
            var verb = @checked ? "check" : "uncheck";
            if (await element.IsCheckedAsync().ConfigureAwait(false) == @checked)
            {
                return;
            }

            if (!@checked && await element.EvaluateAsync<bool>(PageScript.IsRadio).ConfigureAwait(false))
            {
                throw new EngineException(EngineErrorCodes.NotActionable, "uncheck cannot clear a radio button; select another radio in its group", retryable: false);
            }

            await element.ClickAsync(new ElementHandleClickOptions { Timeout = budget.PlaywrightTimeout }).ConfigureAwait(false);
            EngineException Unchanged(string detail = "") =>
                new(EngineErrorCodes.NotActionable, verb + " clicked the control but its checked state did not change" + detail, retryable: false);
            var firstRead = true;
            while (true)
            {
                try
                {
                    if (await element.IsCheckedAsync().ConfigureAwait(false) == @checked)
                    {
                        return;
                    }
                }
                catch (PlaywrightException ex) when (WebErrors.IsDetached(ex) || WebErrors.IsNavigationRace(ex))
                {
                    if (firstRead)
                    {
                        return;
                    }

                    throw Unchanged(" before the control was replaced");
                }

                firstRead = false;

                // No sleep runs past the poll window, so the last read starts at its end and never sees a state that arrived later.
                var left = budget.PollWindowLeft;
                if (left <= TimeSpan.Zero)
                {
                    throw Unchanged();
                }

                // Rounded up to whole milliseconds, which is what Task.Delay waits, so the capped pause never wakes before the window ends.
                var wait = TimeSpan.FromMilliseconds(Math.Ceiling(left.TotalMilliseconds));
                await budget.DelayAsync(wait < CheckPollInterval ? wait : CheckPollInterval).ConfigureAwait(false);
            }
        }

        // The keys Playwright holds for a click. Null holds none.
        private static IEnumerable<KeyboardModifier>? Held(IReadOnlyList<KeyModifier>? modifiers) =>
            modifiers?.Select(modifier => modifier switch
            {
                KeyModifier.Shift => KeyboardModifier.Shift,
                KeyModifier.Control => KeyboardModifier.Control,
                KeyModifier.Alt => KeyboardModifier.Alt,
                KeyModifier.Meta => KeyboardModifier.Meta,
                _ => KeyboardModifier.ControlOrMeta,
            });

        // The share of the extent a swipe moves, as upstream's swipeDistance ratios: none half, slow three quarters, fast one and a half.
        // A swipe naming no momentum keeps the port's three quarters.
        private static double Stride(Momentum? momentum) => momentum switch
        {
            Momentum.None => 0.5,
            Momentum.Fast => 1.5,
            _ => 0.75,
        };

        private static string Direction(ScrollDirection direction)
        {
            return direction switch
            {
                ScrollDirection.Up => "up",
                ScrollDirection.Left => "left",
                ScrollDirection.Right => "right",
                _ => "down",
            };
        }

        private static SemanticNode ToNode(WebNode dto, string testIdAttribute)
        {
            return new SemanticNode
            {
                Ref = dto.Ref,
                Role = dto.Role,
                Name = dto.Name,
                Text = dto.Text,
                InlineNodes = dto.Inline?.Select(inline => ToNode(inline, testIdAttribute)).ToList() ?? [],
                OwnText = dto.OwnText,
                Value = dto.Secure ? null : dto.Value,
                Selection = dto.Secure ? null : dto.Selection,
                TestId = dto.TestId,
                TestIdAttribute = dto.TestId is null ? null : testIdAttribute,
                Placeholder = dto.Placeholder,
                InputPurpose = dto.InputPurpose,
                Level = dto.Level,
                States = new NodeStates
                {
                    Checked = dto.Checked,
                    Disabled = dto.Disabled,
                    Expanded = dto.Expanded,
                    Focused = dto.Focused,
                    Hidden = dto.Hidden,
                    AriaHidden = dto.AriaHidden,
                    Pressed = dto.Pressed,
                    Secure = dto.Secure,
                    Selected = dto.Selected,
                },
                Attributes = dto.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal),
                WholeHref = dto.WholeHref,
                Rect = dto.Rect is { } rect ? new BoundingBox(rect.X, rect.Y, rect.Width, rect.Height) : null,
                Children = dto.Children?.Select(child => ToNode(child, testIdAttribute)).ToList() ?? [],
            };
        }
    }

    /// <summary>One intercepted request, decided through Playwright.</summary>
    private sealed class PlaywrightRoute : IBrowserRoute
    {
        private readonly IRoute _route;
        private readonly Func<string, IReadOnlyDictionary<string, string>?> _siteHeaders;

        public PlaywrightRoute(IRoute route, Func<string, IReadOnlyDictionary<string, string>?> siteHeaders)
        {
            _route = route;
            _siteHeaders = siteHeaders;
            var request = route.Request;
            Request = new WebRouteRequest
            {
                Url = request.Url,
                Method = request.Method,
                Headers = request.Headers,
                PostData = request.PostData,
            };
        }

        public WebRouteRequest Request { get; }

        /// <summary>Set the moment a decision starts, before it reaches Playwright.</summary>
        public bool Decided { get; private set; }

        public static async Task AbortQuietlyAsync(IRoute route)
        {
            try
            {
                await route.AbortAsync().ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // The request is already settled, or its page closed.
            }
        }

        public Task FulfillAsync(RouteFulfillResponse response) =>
            DecideAsync("route.fulfill", () => _route.FulfillAsync(new RouteFulfillOptions
            {
                Status = response.Status,
                Headers = response.Headers,
                ContentType = response.ContentType,
                Body = response.Body,
                Path = response.Path,
            }));

        public Task ContinueAsync(RouteContinueOverrides overrides)
        {
            // Continue skips every route registered before this one, the site-header
            // route included, so it merges those headers itself.
            var site = _siteHeaders(overrides.Url ?? Request.Url);
            Dictionary<string, string>? headers = null;
            if (overrides.Headers is not null || site is not null)
            {
                headers = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (name, value) in overrides.Headers ?? Request.Headers)
                {
                    headers[name.ToLowerInvariant()] = value;
                }

                foreach (var (name, value) in site ?? Enumerable.Empty<KeyValuePair<string, string>>())
                {
                    headers[name] = value;
                }
            }

            return DecideAsync("route.continue", () => _route.ContinueAsync(new RouteContinueOptions
            {
                Url = overrides.Url,
                Method = overrides.Method,
                Headers = headers,
                PostData = overrides.PostData is null ? null : Encoding.UTF8.GetBytes(overrides.PostData),
            }));
        }

        public Task FallbackAsync() => DecideAsync("route.fallback", () => _route.FallbackAsync());

        public Task AbortAsync() => DecideAsync("route.abort", () => _route.AbortAsync());

        // A Playwright call that fails after the decision aborts the request rather
        // than leaving the page's request pending.
        private async Task DecideAsync(string label, Func<Task> decide)
        {
            Decided = true;
            try
            {
                await decide().ConfigureAwait(false);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                await AbortQuietlyAsync(_route).ConfigureAwait(false);
                throw WebErrors.Translate(ex, label);
            }
        }
    }

    /// <summary>What tests replace: the clock budgets read, and the CDP attach.</summary>
    private sealed record SessionSeams(TimeProvider Clock, Func<IPlaywright, string, TimeSpan, CancellationToken, Task<IBrowser>> ConnectCdp);

    /// <summary>The <c>connect</c> resolvers of a persistent context, and the default contexts earlier attempts rode.</summary>
    private sealed record PersistentConnect(
        Func<CancellationToken, Task<string>> Provision,
        Func<CancellationToken, Task<string>> Reconnect,
        HashSet<string> UsedContexts,
        IBrowserProvider? Provider = null)
    {
        /// <summary>Claims a provisioned browser's default context for this attempt; false when an earlier attempt rode it.</summary>
        public bool Claim(string contextId)
        {
            lock (UsedContexts)
            {
                return UsedContexts.Add(contextId);
            }
        }
    }

    private sealed record TargetIdentity(string TargetId, string? BrowserContextId);

    private sealed record SessionBinding(IBrowser Browser, IBrowserContext Context, IPage? Page, string ContextId, TargetIdentity? Target);

    private sealed class FrameWalk
    {
        /// <summary>False for a read that must leave the page's element map as the last observation set it: the trace's.</summary>
        public bool Publish { get; init; } = true;

        public int Remaining { get; set; } = ObservationLimits.Nodes;

        public bool Truncated { get; set; }

        public string? Scroll { get; set; }

        public TraceViewport? Viewport { get; set; }

        public Dictionary<string, IFrame> Frames { get; } = new(StringComparer.Ordinal);
    }

    private sealed class WebViewportDto
    {
        public int Width { get; set; }

        public int Height { get; set; }
    }

    private sealed class WebObservation
    {
        public int Next { get; set; }

        public int Count { get; set; }

        public bool Truncated { get; set; }

        public string? Scroll { get; set; }

        public WebViewportDto? Viewport { get; set; }

        public List<WebNode>? Roots { get; set; }
    }

    private sealed class WebNode
    {
        public string Ref { get; set; } = "";

        public string? Role { get; set; }

        public string? Name { get; set; }

        public string? Text { get; set; }

        public string? OwnText { get; set; }

        public string? Value { get; set; }

        public string? Selection { get; set; }

        public string? TestId { get; set; }

        public string? Placeholder { get; set; }

        public string? InputPurpose { get; set; }

        public int? Level { get; set; }

        public bool Disabled { get; set; }

        public bool Checked { get; set; }

        public bool Expanded { get; set; }

        public bool Selected { get; set; }

        public bool Pressed { get; set; }

        public bool Focused { get; set; }

        public bool Hidden { get; set; }

        public bool AriaHidden { get; set; }

        public bool Secure { get; set; }

        public bool Frame { get; set; }

        public Dictionary<string, string>? Attributes { get; set; }

        public string? WholeHref { get; set; }

        public WebRect? Rect { get; set; }

        public List<WebNode>? Inline { get; set; }

        public List<WebNode>? Children { get; set; }
    }

    private sealed class WebRect
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }
}

/// <summary>
/// The live page and context behind a web session. Each accessor reads the current binding, so it follows a reconnect.
/// <c>ArtifactPath</c> reserves a path under the attempt's artifact directory (upstream's <c>surface.artifactPath</c>).
/// </summary>
internal sealed record LiveSurface(Func<IPage> Page, Func<IBrowserContext> Context, Func<string, string?, string, (string Relative, string Absolute)> ArtifactPath);

/// <summary>
/// One web operation's budget, as upstream <c>withOperationDeadline</c>: one deadline
/// every page call in the operation shares. A call still pending at the deadline is
/// abandoned as <c>OPERATION_TIMEOUT</c> instead of holding the test until its own
/// timeout.
/// </summary>
internal sealed class OperationBudget
{
    /// <summary>
    /// How much sooner than the deadline Playwright's own timeout fires: its error names
    /// what blocked an action, or that the input was already dispatched, and this lead
    /// lets that answer arrive before the deadline does. The deadline is for the calls
    /// Playwright never answers: an evaluate, which takes no timeout, and input on a page
    /// whose renderer is stuck in a script. A budget shorter than twice the lead splits
    /// in half.
    /// </summary>
    private const double PlaywrightTimeoutLeadMs = 250;

    private readonly long _startedAt;
    private readonly TimeSpan _timeout;
    private readonly string _label;
    private readonly CancellationToken _cancellationToken;
    private readonly TimeProvider _clock;

    // startedAt is when the operation began, on the clock, when engine work such as a reconnect already spent part of its budget.
    public OperationBudget(TimeSpan timeout, string label, CancellationToken cancellationToken, TimeProvider? clock = null, long? startedAt = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _clock = clock ?? TimeProvider.System;
        _startedAt = startedAt ?? _clock.GetTimestamp();
        _timeout = timeout;
        _label = label;
        _cancellationToken = cancellationToken;
    }

    /// <summary>The time left before Playwright's own timeout: the one to pass Playwright.</summary>
    public float PlaywrightTimeout
    {
        get
        {
            var left = Remaining().TotalMilliseconds;
            return (float)Math.Ceiling(left - Math.Min(PlaywrightTimeoutLeadMs, left / 2));
        }
    }

    /// <summary>Waits for <paramref name="call"/> until the deadline, then abandons it as <c>OPERATION_TIMEOUT</c>.</summary>
    public async Task WithinAsync(Task call)
    {
        try
        {
            await call.WaitAsync(Remaining(), _clock, _cancellationToken).ConfigureAwait(false);
        }
        catch (System.TimeoutException ex) when (ex != call.Exception?.InnerException)
        {
            // The deadline ran out, not Playwright's own timeout, which keeps its translation.
            throw Timeout();
        }
    }

    /// <inheritdoc cref="WithinAsync(Task)"/>
    public async Task<T> WithinAsync<T>(Task<T> call)
    {
        await WithinAsync((Task)call).ConfigureAwait(false);
        return await call.ConfigureAwait(false);
    }

    /// <summary>Throws <c>OPERATION_TIMEOUT</c> once the deadline has passed, before a call that takes no timeout of its own.</summary>
    public void ThrowIfExpired() => Remaining();

    /// <summary>
    /// The time left to poll in, zero once it has run out. It ends the lead before the
    /// deadline, as <see cref="PlaywrightTimeout"/> does, so a poll fails with its own
    /// answer instead of racing the deadline that abandons it. Unlike the other members, it
    /// does not throw at the deadline.
    /// </summary>
    public TimeSpan PollWindowLeft
    {
        get
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var lead = TimeSpan.FromMilliseconds(Math.Min(PlaywrightTimeoutLeadMs, _timeout.TotalMilliseconds / 4));
            var left = _timeout - lead - _clock.GetElapsedTime(_startedAt);
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    /// <summary>Sleeps on the budget's clock, ending early when the operation is cancelled.</summary>
    public Task DelayAsync(TimeSpan delay) => Task.Delay(delay, _clock, _cancellationToken);

    private TimeSpan Remaining()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var left = _timeout - _clock.GetElapsedTime(_startedAt);
        return left > TimeSpan.Zero ? left : throw Timeout();
    }

    private EngineException Timeout() => new(EngineErrorCodes.OperationTimeout, _label + " timed out", retryable: false);
}

/// <summary>
/// Maps Playwright failures to stable engine codes, following upstream <c>@e2e-dev/web</c>
/// (<c>classifyActionError</c>, <c>translatePwError</c>, <c>navigationStaleOr</c>, and <c>staleOr</c>).
/// </summary>
internal static partial class WebErrors
{
    /// <summary>Whether Playwright raised <paramref name="cause"/>. Playwright for .NET reports timeouts as <see cref="System.TimeoutException"/>.</summary>
    public static bool IsPlaywright(Exception cause)
    {
        return cause is PlaywrightException or System.TimeoutException;
    }

    /// <summary>A failure's message without terminal color sequences.</summary>
    public static string Message(Exception cause)
    {
        return AnsiPattern().Replace(cause.Message, "");
    }

    /// <summary>An unexpected failure: a timeout becomes <c>OPERATION_TIMEOUT</c>, anything else <c>ENGINE_FAILURE</c>.</summary>
    public static EngineException Translate(Exception cause, string label)
    {
        if (cause is EngineException classified)
        {
            return classified;
        }

        var text = label + ": " + Message(cause);
        return cause is System.TimeoutException
            ? new EngineException(EngineErrorCodes.OperationTimeout, text, cause)
            : new EngineException(EngineErrorCodes.EngineFailure, text, cause);
    }

    /// <summary>Whether Playwright's words for <paramref name="cause"/> say the element left the DOM.</summary>
    public static bool IsDetached(Exception cause)
    {
        return DetachedPattern().IsMatch(Message(cause));
    }

    /// <summary>Whether a Playwright failure describes a read that lost its document to a navigation.</summary>
    public static bool IsNavigationRace(Exception cause)
    {
        return NavigationRacePattern().IsMatch(Message(cause));
    }

    /// <summary>A read that lost its document to a navigation is a retryable <c>NODE_STALE</c>, so the caller observes the new document.</summary>
    public static EngineException NavigationStaleOr(Exception cause, string label)
    {
        if (cause is not EngineException && cause is not System.TimeoutException && IsNavigationRace(cause))
        {
            return new EngineException(EngineErrorCodes.NodeStale, label + ": " + Message(cause), retryable: true, cause);
        }

        return Translate(cause, label);
    }

    /// <summary>
    /// Like <see cref="Translate"/>, but detachment and miss failures become retryable <c>NODE_STALE</c>. A timeout stays a
    /// timeout: <c>locate</c> resolves once and never waits, so a timeout there is a budget that ran out, not a miss.
    /// </summary>
    public static Exception StaleOr(Exception cause, string label)
    {
        if (cause is E2EException)
        {
            return cause;
        }

        if (cause is System.TimeoutException)
        {
            return Translate(cause, label);
        }

        var text = Message(cause);
        if (StalePattern().IsMatch(text) || IsNavigationRace(cause))
        {
            return new EngineException(EngineErrorCodes.NodeStale, label + ": " + text, retryable: true, cause);
        }

        return Translate(cause, label);
    }

    /// <summary>Classifies the failure of an input call with no element behind it: a keystroke or a pointer event.</summary>
    public static EngineException ClassifyInput(Exception cause, string label)
    {
        return InputCutOff(cause, label) ?? Translate(cause, label);
    }

    /// <summary>Classifies a failed locator action. A timeout after the input was dispatched may have committed.</summary>
    public static E2EException ClassifyAction(Exception rawCause, LocatorAction action)
    {
        var kind = action.GetType().Name.ToLowerInvariant();
        if (InputCutOff(rawCause, kind) is { } cut)
        {
            return cut;
        }

        if (rawCause is E2EException classified)
        {
            return classified;
        }

        var sensitive = action is LocatorAction.Fill { Sensitive: true };
        var text = Message(rawCause);
        if (action is LocatorAction.Fill fill && sensitive && fill.Value.Length > 0)
        {
            // The message can quote the value in any spelling or cut short.
            text = Redactor.ForValue(fill.Value).RedactFragments(text);
        }

        var cause = sensitive ? null : rawCause;
        // A strict mode violation is a located match that turned ambiguous: stale before any input, so the runner
        // locates again; possibly committed once the call log shows the input went out.
        if (StrictModePattern().IsMatch(text))
        {
            return PostDispatchPattern().IsMatch(text)
                ? new EngineException(EngineErrorCodes.ActionMayHaveCommitted, kind + " matched more than one element after its input was dispatched: " + text, retryable: false, cause)
                : new EngineException(EngineErrorCodes.NodeStale, text, retryable: true, cause);
        }

        if (DetachedPattern().IsMatch(text) || NavigationRacePattern().IsMatch(text))
        {
            return new EngineException(EngineErrorCodes.NodeStale, text, retryable: true, cause);
        }

        if (TimeoutPattern().IsMatch(text) || rawCause is System.TimeoutException)
        {
            if (PostDispatchPattern().IsMatch(text))
            {
                return new EngineException(EngineErrorCodes.ActionMayHaveCommitted, kind + " timed out after its input was dispatched: " + text, retryable: false, cause);
            }

            return new EngineException(EngineErrorCodes.NotActionable, kind + " did not become actionable in time: " + Summary(text), retryable: false, cause);
        }

        if (NotEditablePattern().IsMatch(text))
        {
            return new EngineException(EngineErrorCodes.NotActionable, text, retryable: false, cause);
        }

        return new EngineException(EngineErrorCodes.EngineFailure, text, retryable: false, cause);
    }

    // An input call the operation deadline cut off: Playwright never answered, so the input may have reached the page and must not be repeated blindly.
    private static EngineException? InputCutOff(Exception cause, string label)
    {
        return cause is EngineException { Code: EngineErrorCodes.OperationTimeout }
            ? new EngineException(EngineErrorCodes.ActionMayHaveCommitted, label + " timed out before the page answered; its input may have been dispatched", retryable: false, cause)
            : null;
    }

    // The headline and the last blocker the call log names; a log without a blocker is kept whole.
    private static string Summary(string text)
    {
        var lines = text.Split('\n');
        var blocker = lines.Skip(1)
            .Select(line => line.Trim())
            .Select(line => line.StartsWith("- ", StringComparison.Ordinal) ? line[2..] : line)
            .LastOrDefault(line => BlockerPattern().IsMatch(line));
        return blocker is null ? text : lines[0] + " " + blocker;
    }

    [GeneratedRegex(@"\u001b\[\d+(?:;\d+)*m")]
    private static partial Regex AnsiPattern();

    [GeneratedRegex("strict mode violation", RegexOptions.IgnoreCase)]
    private static partial Regex StrictModePattern();

    [GeneratedRegex("element (is |was )?(detached|not attached)", RegexOptions.IgnoreCase)]
    private static partial Regex DetachedPattern();

    [GeneratedRegex("detached|not attached|resolved to hidden|no element|not found", RegexOptions.IgnoreCase)]
    private static partial Regex StalePattern();

    [GeneratedRegex("Timeout .*exceeded", RegexOptions.IgnoreCase)]
    private static partial Regex TimeoutPattern();

    // Only whole "- " call log lines count, so a locator or element text quoting these words never does.
    [GeneratedRegex(@"^\s*- (performing \w+ action|[\w ]+ action done|waiting for scheduled navigations to finish)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex PostDispatchPattern();

    // A call log line naming what kept a node from being acted on: an element over it, or a state it never reached.
    [GeneratedRegex("^element is (?:not (?:visible|enabled|stable|editable)|outside of the viewport)$| intercepts pointer events$", RegexOptions.IgnoreCase)]
    private static partial Regex BlockerPattern();

    [GeneratedRegex("not an? <?(input|checkbox|radio|select)|not editable|not checkable", RegexOptions.IgnoreCase)]
    private static partial Regex NotEditablePattern();

    [GeneratedRegex("execution context was destroyed|because of a navigation|navigating and changing the content|frame was detached|frame got detached|node is detached from document", RegexOptions.IgnoreCase)]
    private static partial Regex NavigationRacePattern();
}
