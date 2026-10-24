// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.WinApp;

/// <summary>The pattern action <see cref="IWindowDriver.InvokeAsync"/> performs. <see cref="Auto"/> lets the library pick the element's own pattern.</summary>
public enum InvokeKind
{
    Auto,
    Invoke,
    Select,
    Toggle,
    ToggleOn,
    ToggleOff,
    Expand,
    Collapse,
}

/// <summary>What <see cref="IWindowDriver.ShowAsync"/> puts a window in.</summary>
public enum WindowState
{
    Normal,
    Maximized,
    Minimized,
}

/// <summary>Why a driver call failed, as the engine classifies it. <see cref="WinAppErrors"/> maps each one onto an engine error code.</summary>
public enum DriverFault
{
    /// <summary>The element a handle named is gone from the tree.</summary>
    ElementVanished,

    /// <summary>A name matched several elements.</summary>
    Ambiguous,

    /// <summary>The element is off the screen, so a pointer cannot reach it.</summary>
    Offscreen,

    /// <summary>No process or window matched the app.</summary>
    AppNotFound,

    /// <summary>Another window took the foreground while input was being injected.</summary>
    ForegroundLost,

    /// <summary>The element refused the value (no value pattern, or a read-only one).</summary>
    ValueRejected,

    /// <summary>The app could not be started, or opened no window in time.</summary>
    Launch,

    Other,
}

/// <summary>A driver failure with its classification. The message is the library's own text.</summary>
public sealed class WindowDriverException(DriverFault fault, string message, Exception? cause = null) : Exception(message, cause)
{
    public DriverFault Fault { get; } = fault;
}

/// <summary>
/// What the engine asks of Windows: the one seam between the platform-neutral session and the winapp UI Automation library,
/// which is Windows-only. <c>UiAutomationDriver</c> implements it over the library; unit tests script it. Every member takes
/// the caller's cancellation token and lets its cancellation surface as <see cref="OperationCanceledException"/>. Every other
/// failure is a <see cref="WindowDriverException"/>. Points and bounds are in physical screen pixels.
/// </summary>
public interface IWindowDriver : IAsyncDisposable
{
    /// <summary>
    /// Starts <paramref name="executable"/> and waits until the process shows a visible top-level window, within <paramref name="timeout"/>.
    /// What the process writes to its output goes to <paramref name="log"/>. A process that exits first, or shows no window in time, is
    /// ended and fails with <see cref="DriverFault.Launch"/>.
    /// </summary>
    Task<LaunchedApp> LaunchAsync(string executable, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan timeout, Action<AppLogEntry>? log, CancellationToken cancellationToken);

    /// <summary>Finds a running app by process name, process id, or window title. <see cref="DriverFault.AppNotFound"/> when none matches.</summary>
    Task<LaunchedApp> AttachAsync(string app, CancellationToken cancellationToken);

    /// <summary>Ends the app's process tree and waits for it, no longer than <paramref name="timeout"/>.</summary>
    Task CloseAsync(LaunchedApp app, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>The visible top-level windows of a process with a non-empty box.</summary>
    Task<IReadOnlyList<WindowInfo>> WindowsAsync(int processId, CancellationToken cancellationToken);

    /// <summary>The element tree of one window, as deep as the library walks.</summary>
    Task<RawElement> InspectAsync(long windowHandle, CancellationToken cancellationToken);

    /// <summary>The handle of the element that holds keyboard focus in a window, or null.</summary>
    Task<string?> FocusedAsync(long windowHandle, CancellationToken cancellationToken);

    /// <summary>Performs a pattern action on an element. Answers the pattern that ran.</summary>
    Task<string> InvokeAsync(string handle, InvokeKind kind, CancellationToken cancellationToken);

    Task SetValueAsync(string handle, string text, CancellationToken cancellationToken);

    Task FocusAsync(string handle, CancellationToken cancellationToken);

    Task ScrollIntoViewAsync(string handle, CancellationToken cancellationToken);

    /// <summary>Scrolls a scrollable element one step in <paramref name="direction"/> through its scroll pattern.</summary>
    Task ScrollAsync(string handle, ScrollDirection direction, CancellationToken cancellationToken);

    /// <summary>Sends keys to the window, in the library's key grammar, or as literal text when <paramref name="verbatim"/>.</summary>
    Task SendKeysAsync(long windowHandle, string keys, bool verbatim, CancellationToken cancellationToken);

    /// <summary>Clicks at a point: a left click, a double left click, or a right click.</summary>
    Task ClickAsync(Point screen, bool doubleClick, bool rightClick, CancellationToken cancellationToken);

    Task HoverAsync(Point screen, CancellationToken cancellationToken);

    /// <summary>Presses at <paramref name="from"/>, holds for <paramref name="hold"/>, moves to <paramref name="to"/>, and releases.</summary>
    Task DragAsync(Point from, Point to, TimeSpan hold, CancellationToken cancellationToken);

    /// <summary>Turns the wheel at a point: positive notches scroll up, negative down.</summary>
    Task ScrollWheelAsync(Point screen, int notches, CancellationToken cancellationToken);

    /// <summary>Brings a window to the foreground, restoring it when minimized.</summary>
    Task ForegroundAsync(long windowHandle, CancellationToken cancellationToken);

    Task<RawScreenshot> ScreenshotAsync(long windowHandle, CancellationToken cancellationToken);

    /// <summary>Resizes a window to a logical size.</summary>
    Task ResizeAsync(long windowHandle, int width, int height, CancellationToken cancellationToken);

    Task ShowAsync(long windowHandle, WindowState state, CancellationToken cancellationToken);
}
