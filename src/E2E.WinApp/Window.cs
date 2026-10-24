// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.WinApp;

/// <summary>What <see cref="Window.AlertAsync"/> does with the open dialog.</summary>
public enum AlertAction
{
    Accept,
    Dismiss,
}

/// <summary>
/// The window fixture: deterministic app and window management this engine contributes. It is not an agent tool. A test calls
/// these directly to arrange or assert window state. Every method reads the cancellation token of its own call, so a fixture
/// follows a harness from the test body into its teardown. The mobile engine's <c>Device</c>, for Windows.
/// </summary>
public sealed class Window
{
    /// <summary>Automation ids of the buttons a WinUI <c>ContentDialog</c> names, by what each one does.</summary>
    private static readonly string[] AcceptIds = ["PrimaryButton"];
    private static readonly string[] DismissIds = ["CloseButton", "SecondaryButton"];
    private static readonly string[] AcceptNames = ["OK", "Yes", "Confirm", "Accept", "Save", "Continue"];

    private readonly WindowSession _session;

    internal Window(WindowSession session)
    {
        _session = session;
    }

    /// <summary>
    /// Brings an app to the foreground. The app under test, named by its process name or executable, is launched fresh with
    /// <see cref="OpenAppOptions.Relaunch"/>, or when it is not running. Its launch arguments apply to this launch alone; the target's own
    /// apply to a relaunch of the app under test.
    /// </summary>
    public Task OpenAppAsync(string? app = null, OpenAppOptions? options = null, CancellationToken cancellationToken = default) =>
        _session.OpenAppAsync(app ?? _session.AppName ?? throw WinAppSupport.Unsupported("openApp needs an app, or the target's `app.appPath` or `app.bundleId`"), options, cancellationToken);

    /// <summary>Ends the app the session started, so the next <see cref="OpenAppAsync"/> launches it fresh. Until then the screen is unobservable.</summary>
    public Task CloseAppAsync(CancellationToken cancellationToken = default) => _session.CloseAppAsync(cancellationToken);

    /// <summary>The app the session is on and the title of its main window.</summary>
    public Task<ForegroundApp> ForegroundAppAsync(CancellationToken cancellationToken = default) => _session.ForegroundAppAsync(cancellationToken);

    /// <summary>The app's visible top-level windows, with their titles and boxes.</summary>
    public Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken = default) => _session.WindowsAsync(cancellationToken);

    /// <summary>Resizes the main window to a logical size, in device-independent pixels. The next observation reads the new layout.</summary>
    public async Task ResizeAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var window = _session.MainWindow;
        await _session.CommandAsync("window.resize", (driver, token) => driver.ResizeAsync(window, width, height, token), cancellationToken).ConfigureAwait(false);
        await _session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Maximizes the main window.</summary>
    public Task MaximizeAsync(CancellationToken cancellationToken = default) => ShowAsync("window.maximize", WindowState.Maximized, cancellationToken);

    /// <summary>Restores the main window to its normal size.</summary>
    public Task RestoreAsync(CancellationToken cancellationToken = default) => ShowAsync("window.restore", WindowState.Normal, cancellationToken);

    /// <summary>Minimizes the main window. Keys and clicks need it back: <see cref="RestoreAsync"/> or <see cref="OpenAppAsync"/> bring it forward.</summary>
    public Task MinimizeAsync(CancellationToken cancellationToken = default) => ShowAsync("window.minimize", WindowState.Minimized, cancellationToken);

    /// <summary>
    /// Accepts or dismisses the open dialog, as the last observation saw it: a node of role <c>dialog</c>. Accept presses the
    /// dialog's primary button (a <c>ContentDialog</c>'s <c>PrimaryButton</c>, else a button named OK, Yes, Confirm, Accept, Save, or Continue); dismiss presses its close or
    /// secondary button, else sends Escape to the window. <c>INVALID_STATE</c> when no dialog is open. The buttons a dialog lists
    /// can be tapped by id too, and that tap replays from the trace cache; this does not.
    /// </summary>
    public async Task AlertAsync(AlertAction action, CancellationToken cancellationToken = default)
    {
        var dialog = _session.Latest.LastOrDefault(entry => entry.Role == "dialog" && !entry.Node.States.Hidden)
            ?? throw WinAppSupport.InvalidState("no dialog is open; observe the screen first, and tap a listed button by id when the dialog has one");
        var buttons = _session.Latest.Where(entry => entry.Role == "button" && IsWithin(entry, dialog)).ToList();
        var wanted = action == AlertAction.Accept ? AcceptIds : DismissIds;
        var button = buttons.FirstOrDefault(entry => wanted.Contains(entry.Raw.AutomationId ?? "", StringComparer.Ordinal))
            ?? (action == AlertAction.Accept ? buttons.FirstOrDefault(entry => AcceptNames.Contains(entry.Node.Name ?? "", StringComparer.OrdinalIgnoreCase)) : null);
        if (button is not null)
        {
            await _session.CommandAsync("window.alert", (driver, token) => driver.InvokeAsync(button.Handle, InvokeKind.Invoke, token), cancellationToken).ConfigureAwait(false);
        }
        else if (action == AlertAction.Dismiss)
        {
            var window = dialog.WindowHandle;
            // The key goes through SendInput, which reaches the foreground window only.
            await _session.CommandAsync(
                "window.alert",
                async (driver, token) =>
                {
                    await driver.ForegroundAsync(window, token).ConfigureAwait(false);
                    await driver.SendKeysAsync(window, "esc", verbatim: false, token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw WinAppSupport.NotActionable("the open dialog has no button to accept it with");
        }

        await _session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether <paramref name="entry"/> is inside <paramref name="ancestor"/>, by the trees the last observation reported.</summary>
    private bool IsWithin(NodeBinding entry, NodeBinding ancestor)
    {
        if (entry.WindowHandle != ancestor.WindowHandle)
        {
            return false;
        }

        return Contains(ancestor.Node, entry.Id);
    }

    private static bool Contains(SemanticNode node, string id) =>
        node.Children.Any(child => child.Ref == id || Contains(child, id));

    private async Task ShowAsync(string label, WindowState state, CancellationToken cancellationToken)
    {
        var window = _session.MainWindow;
        await _session.CommandAsync(label, (driver, token) => driver.ShowAsync(window, state, token), cancellationToken).ConfigureAwait(false);
        await _session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }
}
