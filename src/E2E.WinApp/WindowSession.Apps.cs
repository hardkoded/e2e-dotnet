// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.WinApp;

/// <summary>How <see cref="WindowSession.OpenAppAsync"/> opens an app.</summary>
public sealed record OpenAppOptions
{
    /// <summary>End the app first, so it starts fresh. Without it a running app is brought forward as it is.</summary>
    public bool Relaunch { get; init; }

    /// <summary>Arguments this launch hands the executable. Replaces the target's <c>LaunchArguments</c> for a relaunch of the app under test.</summary>
    public IReadOnlyList<string>? LaunchArguments { get; init; }
}

/// <summary>The app the session is on.</summary>
/// <param name="Name">The app's name: its executable's name, or the process name it was attached by.</param>
/// <param name="BundleId">The title of its main window, when it has one.</param>
public sealed record ForegroundApp(string Name, string? BundleId = null);

public sealed partial class WindowSession
{
    /// <summary>
    /// Opens an app. The app under test, named by the target's process name or executable, is launched fresh when the target
    /// has a build and <paramref name="options"/> say relaunch, or when it is not running; otherwise it is brought forward.
    /// Any other name is a running app, found by process name, window title, or process id, and brought forward.
    /// </summary>
    public async Task OpenAppAsync(string app, OpenAppOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        options ??= new OpenAppOptions();
        var isPinned = IsPinned(app);
        if (isPinned && AppPath is { } executable && (options.Relaunch || _app is null))
        {
            await LaunchAsync(executable, options.LaunchArguments ?? Options.App.LaunchArguments ?? [], cancellationToken).ConfigureAwait(false);
            return;
        }

        if (isPinned && _app is { } running && !options.Relaunch)
        {
            await ForegroundAsync(running, cancellationToken).ConfigureAwait(false);
            return;
        }

        var attached = await CommandAsync($"open {app}", (driver, token) => driver.AttachAsync(app, token), cancellationToken).ConfigureAwait(false);
        if (isPinned)
        {
            // Attach mode: the target names a running app by process name and no build to launch. The session never ends an app it did not start.
            Adopt(attached);
        }

        await CommandAsync($"open {app}", (driver, token) => driver.ForegroundAsync(attached.MainWindow, token), cancellationToken).ConfigureAwait(false);
        await SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ends the app this session started, so the next <see cref="OpenAppAsync"/> launches it fresh. Until then the screen is unobservable. An attached app is left running and forgotten.</summary>
    public async Task CloseAppAsync(CancellationToken cancellationToken = default)
    {
        var app = _app;
        _app = null;
        _mainWindow = null;
        ClearBindings();
        if (app is { Owned: true })
        {
            await CommandAsync("closeApp", (driver, token) => driver.CloseAsync(app, E2EDefaults.CleanupTimeout, token), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The app the session is on and the title of its main window, from the windows as they are now.</summary>
    public async Task<ForegroundApp> ForegroundAppAsync(CancellationToken cancellationToken = default)
    {
        var app = _app ?? throw WinAppSupport.InvalidState("no app is open; call app.open()");
        var windows = await CommandAsync("foregroundApp", (driver, token) => driver.WindowsAsync(app.ProcessId, token), cancellationToken).ConfigureAwait(false);
        var main = windows.FirstOrDefault(window => window.Handle == app.MainWindow) ?? Largest(windows);
        return new ForegroundApp(app.Name, main is { Title.Length: > 0 } ? main.Title : null);
    }

    /// <summary>The app's visible top-level windows.</summary>
    public async Task<IReadOnlyList<WindowInfo>> WindowsAsync(CancellationToken cancellationToken = default)
    {
        var app = _app ?? throw WinAppSupport.InvalidState("no app is open; call app.open()");
        return await CommandAsync("windows", (driver, token) => driver.WindowsAsync(app.ProcessId, token), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>One fresh launch of the executable: the running one ends first, the new one is adopted once it shows a window.</summary>
    private async Task LaunchAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (_app is { Owned: true } previous)
        {
            _app = null;
            _mainWindow = null;
            await CommandAsync("close the running app", (driver, token) => driver.CloseAsync(previous, E2EDefaults.CleanupTimeout, token), cancellationToken).ConfigureAwait(false);
        }

        ClearBindings();
        var workingDirectory = Path.GetDirectoryName(executable) ?? _projectRoot;
        var launched = await CommandAsync(
            $"launch {Path.GetFileName(executable)}",
            (driver, token) => driver.LaunchAsync(executable, arguments, workingDirectory, Options.LaunchTimeout, Log, token),
            cancellationToken).ConfigureAwait(false);
        Adopt(launched);
        await CommandAsync("foreground the app", (driver, token) => driver.ForegroundAsync(launched.MainWindow, token), cancellationToken).ConfigureAwait(false);
        await SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ForegroundAsync(LaunchedApp app, CancellationToken cancellationToken)
    {
        var window = await MainWindowAsync(app, cancellationToken).ConfigureAwait(false);
        await CommandAsync($"open {app.Name}", (driver, token) => driver.ForegroundAsync(window, token), cancellationToken).ConfigureAwait(false);
        await SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The window the launch found, or the largest window of the process when that one closed; <c>INVALID_STATE</c> when the app has none.</summary>
    internal async Task<long> MainWindowAsync(LaunchedApp app, CancellationToken cancellationToken)
    {
        var windows = await CommandAsync("windows", (driver, token) => driver.WindowsAsync(app.ProcessId, token), cancellationToken).ConfigureAwait(false);
        var main = windows.FirstOrDefault(window => window.Handle == app.MainWindow) ?? Largest(windows)
            ?? throw WinAppSupport.InvalidState($"{app.Name} has no window open; call app.open()");
        _mainWindow = main.Handle;
        return main.Handle;
    }

    private void Adopt(LaunchedApp app)
    {
        _app = app;
        _mainWindow = app.MainWindow;
        ClearBindings();
    }

    /// <summary>Whether <paramref name="app"/> names the app under test: its process name, its executable's name or path.</summary>
    private bool IsPinned(string app)
    {
        if (AppName is { } name && string.Equals(app, name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (AppPath is { } path)
        {
            if (string.Equals(app, Options.App.AppPath, StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetFullPath(app, _projectRoot), path, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var file = Path.GetFileName(path);
            if (string.Equals(app, file, StringComparison.OrdinalIgnoreCase) || string.Equals(app, Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static WindowInfo? Largest(IReadOnlyList<WindowInfo> windows) =>
        windows.OrderByDescending(window => window.Bounds.Width * window.Bounds.Height).FirstOrDefault();
}
