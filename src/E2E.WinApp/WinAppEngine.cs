// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using E2E.Engine;

namespace E2E.WinApp;

/// <summary>What <see cref="WinAppEngine"/> hands the code that turns a node and an action into driver calls.</summary>
/// <remarks><see cref="WindowActions"/> is the implementation the engine uses unless a caller sets another through <see cref="WinAppEngine.Actions"/>.</remarks>
public interface IWindowActions
{
    Task PerformAsync(WindowSession session, SemanticNode node, LocatorAction action, CancellationToken cancellationToken);

    Task PressAsync(WindowSession session, string key, CancellationToken cancellationToken);

    Task SwipeAsync(WindowSession session, ScrollDirection direction, CancellationToken cancellationToken);

    Task<EngineScreenshot> ScreenshotAsync(WindowSession session, CancellationToken cancellationToken);
}

/// <summary>
/// The Windows app engine for e2e: a desktop body that implements the core engine contract over Windows UI Automation, through
/// the library behind the <c>winapp ui</c> command. A test launches the pinned app with <c>app.open()</c>, which is the fresh
/// launch of the executable the target names, or attaches to the running app it names. There is no upstream counterpart:
/// upstream has no desktop engine. The surface follows upstream's <c>@e2e-dev/mobile</c>, the other pinned-app engine.
/// </summary>
public sealed class WinAppEngine : IEngine
{
    /// <summary>The version of the engine.</summary>
    public const string EngineVersion = "0.1.0";

    /// <summary>The winapp UI Automation library this package drives.</summary>
    public const string LibraryVersion = "0.7.1";

    private readonly Func<IWindowDriver> _createDriver;
    private readonly List<WindowSession> _sessions = [];

    /// <summary>Creates the engine over Windows UI Automation. Starting a session on another operating system fails with <c>ENVIRONMENT_UNAVAILABLE</c>.</summary>
    public WinAppEngine(WinAppOptions options)
        : this(options, CreateDriver)
    {
    }

    /// <summary>Creates the engine over a driver of the caller's making: the seam unit tests script.</summary>
    internal WinAppEngine(WinAppOptions options, Func<IWindowDriver> createDriver)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(createDriver);
        Validate(options);
        Options = options;
        _createDriver = createDriver;
    }

    public WinAppOptions Options { get; }

    /// <summary>The code that performs node actions, keys, scrolls, and screenshots.</summary>
    public IWindowActions Actions { get; init; } = new WindowActions();

    /// <summary>The clock the cleanup budget counts on. Tests replace it.</summary>
    internal TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>The settle wait after an input. Tests replace it to spend no time.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = static (span, token) => Task.Delay(span, token);

    /// <summary>The sessions this engine started that have not closed: the attempts running on it right now.</summary>
    public IReadOnlyList<WindowSession> ActiveSessions
    {
        get
        {
            lock (_sessions)
            {
                return [.. _sessions];
            }
        }
    }

    public string Platform => "windows";

    public string Version => EngineVersion;

    /// <summary>
    /// No <see cref="EngineCapabilities.Location"/>: core resolves locators over <see cref="WindowSession.ObserveAsync"/>. No history: a
    /// desktop app has no back stack the engine can see. No video. No tap modifiers: a pattern invoke holds no keys.
    /// </summary>
    public EngineCapabilities Capabilities =>
        EngineCapabilities.Observation
        | EngineCapabilities.Actions
        | EngineCapabilities.Keyboard
        | EngineCapabilities.Scroll
        | EngineCapabilities.Screenshot
        | EngineCapabilities.PinnedApp;

    /// <summary>
    /// A Windows target launches an executable or attaches to a running app: one of the build path and the app name is required.
    /// A URL is a website, which a desktop app engine cannot open.
    /// </summary>
    public static void ValidateApp(WinAppApp app, string? url, string targetName)
    {
        ArgumentNullException.ThrowIfNull(app);
        var where = $"target \"{targetName}\"";
        if (url is not null)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"{where} declares app.url, but a windows target drives a desktop app, not a website; name the app with app.appPath (an executable) or app.bundleId (a running app's process name)");
        }

        if (app.BundleId is null && app.AppPath is null)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"{where} needs app.appPath or app.bundleId: a windows target launches an executable, targets: [{{ \"platform\": \"windows\", \"app\": {{ \"appPath\": \"bin/MyApp.exe\" }} }}]");
        }
    }

    /// <summary>Starts one attempt: a session over a fresh driver. The app is launched by <c>app.open()</c>, as on a device.</summary>
    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateApp(Options.App, options.BaseUrl, Options.TargetName);
        return await WindowSession.StartAsync(this, _createDriver, options, cancellationToken).ConfigureAwait(false);
    }

    internal void Register(WindowSession session)
    {
        lock (_sessions)
        {
            _sessions.Add(session);
        }
    }

    internal void Unregister(WindowSession session)
    {
        lock (_sessions)
        {
            _sessions.Remove(session);
        }
    }

    /// <summary>The library-backed driver, on Windows. Elsewhere the engine is built but cannot start.</summary>
    private static IWindowDriver CreateDriver()
    {
        if (OperatingSystem.IsWindows())
        {
            return new UiAutomationDriver();
        }

        throw WinAppSupport.Unavailable($"the Windows app engine drives desktop apps through Windows UI Automation and runs on Windows only; this host is {RuntimeInformation.OSDescription}");
    }

    private static void Validate(WinAppOptions options)
    {
        if (options.SettleMilliseconds < 0)
        {
            throw new ConfigurationException("INVALID_CONFIG", "winapp: `settle` must be a non-negative integer of milliseconds, or false");
        }

        if (options.LaunchTimeout <= TimeSpan.Zero)
        {
            throw new ConfigurationException("INVALID_CONFIG", "winapp: `launchTimeout` must be positive");
        }
    }
}
