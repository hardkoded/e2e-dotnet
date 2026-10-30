// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using E2E.Engine;
using E2E.Mobile;

namespace E2E.WinApp;

/// <summary>What the session keeps for one node it reported: the driver handle to act on and the window it lives in.</summary>
/// <param name="Id">The node's ref in the observation.</param>
/// <param name="Handle">The driver's key for the element.</param>
/// <param name="WindowHandle">The top-level window the element sits in.</param>
/// <param name="Node">The node as it was reported.</param>
/// <param name="Raw">The element as the driver read it.</param>
/// <param name="Role">The node's role.</param>
public sealed record NodeBinding(string Id, string Handle, long WindowHandle, SemanticNode Node, RawElement Raw, string? Role);

/// <summary>
/// One attempt's session with a Windows app, driven through an <see cref="IWindowDriver"/> and exposed to the runner as the
/// contract's session. Every observation projects the UI Automation tree of the app's windows under one root of a stable id
/// (<see cref="UiaProjection.RootId"/>). The session owns the id space, the app's process, and every translation between the
/// contract's vocabulary and the driver's calls. The actions themselves are <see cref="WindowActions"/>. The mobile engine's
/// <c>DeviceSession</c>, for Windows.
/// </summary>
public sealed partial class WindowSession : IEngineSession
{
    /// <summary>How many bindings a session keeps; the oldest goes first. Upstream's <c>MAX_LOCATED_REFS</c>.</summary>
    private const int MaxBindings = 2048;

    private readonly WinAppEngine _engine;
    private readonly string _projectRoot;
    private readonly string? _artifactsDir;
    private int _screenshots;
    private readonly Action<AppLogEntry>? _appLog;
    private readonly Action<IReadOnlyDictionary<string, string>>? _environment;

    /// <summary>Bindings of every node reported, newest last, independent of the observation they came from: UI Automation identity is stable across observations, so an older ref is still actionable until its element is gone.</summary>
    private readonly OrderedDictionary<string, NodeBinding> _bindings = [];
    private readonly Lock _bindingsLock = new();
    private IWindowDriver? _driver;
    private int _idCounter;
    private LaunchedApp? _app;
    private long? _mainWindow;
    private ViewportSize? _viewport;
    private double _scale = 1;

    private WindowSession(WinAppEngine engine, EngineStartOptions options, IWindowDriver driver)
    {
        _engine = engine;
        _driver = driver;
        _projectRoot = options.ProjectRoot ?? Directory.GetCurrentDirectory();
        _artifactsDir = options.ArtifactsDir;
        _appLog = options.AppLog;
        _environment = options.Environment;
    }

    public string Route { get; private set; } = "";

    public WinAppOptions Options => _engine.Options;

    /// <summary>The window fixture of this session: deterministic window and app management for a test.</summary>
    public Window Window => field ??= new Window(this);

    /// <summary>The app the session launched or attached to, or null before <c>app.open()</c> and after <c>closeApp</c>.</summary>
    public LaunchedApp? App => _app;

    /// <summary>The executable the target names, resolved against the project root. Null for an attach-only target.</summary>
    public string? AppPath => Options.App.AppPath is null ? null : Path.GetFullPath(Options.App.AppPath, _projectRoot);

    /// <summary>The process name the target names, which an attach finds the app by and <c>open_app</c> knows it as.</summary>
    public string? AppName => Options.App.BundleId ?? (Options.App.AppPath is null ? null : Path.GetFileNameWithoutExtension(Options.App.AppPath));

    /// <summary>The driver of the live session, or <c>INVALID_STATE</c> once it closed.</summary>
    internal IWindowDriver Driver => _driver ?? throw WinAppSupport.InvalidState("the Windows app session is closed");

    /// <summary>The window actions, keys, and screenshots go to: the window the launch found, or the largest window of the app when that one is gone.</summary>
    internal long MainWindow => _mainWindow ?? throw WinAppSupport.InvalidState("no app window is open; call app.open()");

    /// <summary>Physical pixels per device-independent pixel of the main window's monitor, as the last observation measured it.</summary>
    internal double Scale => _scale;

    /// <summary>The main window's logical size, as the last observation measured it.</summary>
    internal ViewportSize? Viewport => _viewport;

    internal static async Task<WindowSession> StartAsync(WinAppEngine engine, Func<IWindowDriver> createDriver, EngineStartOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var driver = createDriver();
        var session = new WindowSession(engine, options, driver);
        engine.Register(session);
        session.TellEnvironment();
        return session;
    }

    /// <summary>Runs one driver call under a cancellation token and translates its failure. Fixture methods route through here too.</summary>
    public Task<T> CommandAsync<T>(string label, Func<IWindowDriver, CancellationToken, Task<T>> run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var driver = Driver;
        return WinAppErrors.RunAsync(label, token => run(driver, token), cancellationToken);
    }

    public Task CommandAsync(string label, Func<IWindowDriver, CancellationToken, Task> run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var driver = Driver;
        return WinAppErrors.RunAsync(label, token => run(driver, token), cancellationToken);
    }

    public Task OpenAsync(string url, CancellationToken cancellationToken)
    {
        // A desktop app has no URL: app.open() is the fresh launch of the pinned app.
        ArgumentNullException.ThrowIfNull(url);
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https" or "file")
        {
            return Task.FromException(WinAppSupport.Unsupported("a Windows app has no URL to open; app.open() launches the app the target names"));
        }

        return RestartAsync(cancellationToken);
    }

    /// <summary>Launches the app fresh: ends the one this session started, starts the executable, and waits for its window. An attach-only target attaches instead.</summary>
    public Task RestartAsync(CancellationToken cancellationToken) =>
        OpenAppAsync(AppName ?? throw WinAppSupport.Unsupported("launching the app needs the target's `app.appPath` or `app.bundleId`"), new OpenAppOptions { Relaunch = true }, cancellationToken);

    /// <summary>
    /// Relaunches the app. A Windows app keeps its state where it chooses (its local app data, the registry, a file), and the engine
    /// does not know where, so this is the fresh launch <see cref="RestartAsync"/> makes and nothing more. A test that needs clean
    /// data removes it itself before <c>app.open()</c>.
    /// </summary>
    public Task ClearStateAsync(CancellationToken cancellationToken) => RestartAsync(cancellationToken);

    public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
        _engine.Actions.PerformAsync(this, node, action, cancellationToken);

    public Task PressAsync(string key, CancellationToken cancellationToken) =>
        _engine.Actions.PressAsync(this, key, cancellationToken);

    public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) =>
        _engine.Actions.SwipeAsync(this, direction, cancellationToken);

    public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) =>
        _engine.Actions.ScreenshotAsync(this, cancellationToken);

    /// <summary>
    /// A redacted screenshot artifact: the main window with every secure field painted over, as <see cref="ScreenshotAsync(CancellationToken)"/>
    /// returns it. An image that cannot be redacted is not written at all. The Windows counterpart of <c>screenshot</c> in upstream's
    /// <c>mobile/src/surface.ts</c>.
    /// </summary>
    public async Task<string> ScreenshotArtifactAsync(string? label, CancellationToken cancellationToken)
    {
        var artifactsDir = _artifactsDir ?? throw WinAppSupport.InvalidState("screenshot outside an attempt");
        var masked = await ScreenshotAsync(cancellationToken).ConfigureAwait(false);
        var name = Interlocked.Increment(ref _screenshots).ToString("D3", System.Globalization.CultureInfo.InvariantCulture)
            + "-" + WebSupport.SanitizeFilename(label ?? "screenshot") + ".png";
        var directory = Path.Combine(artifactsDir, "screenshots");
        Directory.CreateDirectory(directory);
        var absolute = Path.Combine(directory, name);
        var temporary = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, masked.Png, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, absolute, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }

        return "screenshots/" + name;
    }

    public ValueTask DisposeAsync() => new(CloseAsync(E2EDefaults.CleanupTimeout, CancellationToken.None));

    /// <summary>Ends the app this session started and the driver, within a budget: a close that outlives it is abandoned, as one that fails is.</summary>
    internal async Task CloseAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var driver = _driver;
        var app = _app;
        _driver = null;
        _app = null;
        _mainWindow = null;
        _viewport = null;
        _engine.Unregister(this);
        ClearBindings();
        if (driver is null)
        {
            return;
        }

        await CleanupBudget.WithinAsync(
            async token =>
            {
                try
                {
                    if (app is { Owned: true })
                    {
                        await driver.CloseAsync(app, timeout, token).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    // The app is going away either way.
                }
                finally
                {
                    await driver.DisposeAsync().ConfigureAwait(false);
                }
            },
            timeout,
            cancellationToken,
            _engine.Clock).ConfigureAwait(false);
    }

    /// <summary>Waits the settle delay after an input, when the options ask for one.</summary>
    internal Task SettleAsync(CancellationToken cancellationToken) =>
        Options.Settle && Options.SettleMilliseconds > 0
            ? _engine.Delay(TimeSpan.FromMilliseconds(Options.SettleMilliseconds), cancellationToken)
            : Task.CompletedTask;

    /// <summary>The binding of a node this session reported, or <c>NODE_STALE</c> for a ref it never minted or has forgotten.</summary>
    internal NodeBinding Resolve(SemanticNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        lock (_bindingsLock)
        {
            return _bindings.TryGetValue(node.Ref, out var entry)
                ? entry
                : throw new EngineException(EngineErrorCodes.NodeStale, $"node {node.Ref} is not part of the newest observation", retryable: true);
        }
    }

    private readonly HashSet<string> _toggled = [];

    /// <summary>Whether a check or uncheck changed the node since the last observation, which makes its snapshot state stale.</summary>
    internal bool Toggled(string id) { lock (_bindingsLock) { return _toggled.Contains(id); } }

    internal void MarkToggled(string id) { lock (_bindingsLock) { _toggled.Add(id); } }

    /// <summary>The bindings of the last observation, in document order.</summary>
    internal IReadOnlyList<NodeBinding> Latest { get; private set; } = [];

    private void Remember(IReadOnlyList<NodeBinding> bindings)
    {
        lock (_bindingsLock)
        {
            foreach (var entry in bindings)
            {
                // The newest observation goes to the end, so the cap evicts older observations first and never a node of this one.
                _bindings.Remove(entry.Id);
                _bindings[entry.Id] = entry;
            }

            while (_bindings.Count > Math.Max(MaxBindings, bindings.Count))
            {
                _bindings.RemoveAt(0);
            }

            Latest = bindings;
            _toggled.Clear();
        }
    }

    private void ClearBindings()
    {
        lock (_bindingsLock)
        {
            _bindings.Clear();
            Latest = [];
            _toggled.Clear();
        }
    }

    /// <summary>Tells the trace what the attempt runs on. The trace is a reader of the attempt, so a hook that throws never fails it.</summary>
    private void TellEnvironment()
    {
        var facts = new Dictionary<string, string>
        {
            ["platform"] = "windows",
            ["os"] = RuntimeInformation.OSDescription,
            ["uiAutomation"] = WinAppEngine.LibraryVersion,
        };
        try
        {
            _environment?.Invoke(facts);
        }
        catch (Exception)
        {
            // The trace is a reader of the attempt, never a reason it fails.
        }
    }

    /// <summary>One line of what the app wrote, for the attempt's trace. A sink that throws never fails the attempt.</summary>
    private void Log(AppLogEntry entry)
    {
        try
        {
            _appLog?.Invoke(entry);
        }
        catch (Exception)
        {
            // The trace is a reader of the attempt, never a reason it fails.
        }
    }
}
