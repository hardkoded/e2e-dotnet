// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.WinApp;

/// <summary>
/// What a <see cref="WindowSession"/> does for each action of the contract: the translation of a node and an action into driver
/// calls. A pattern action (invoke, toggle, select, expand, set value) goes through UI Automation and needs no foreground window;
/// a pointer or key action goes through input injection, which needs the window in front, and is followed by the settle wait.
/// The mobile engine's <c>DeviceActions</c>, for Windows.
/// </summary>
public sealed class WindowActions : IWindowActions
{
    /// <summary>How long a long press holds when the caller names no duration: long enough for a WinUI <c>Holding</c> gesture.</summary>
    public static readonly TimeSpan DefaultLongPress = TimeSpan.FromSeconds(1);

    /// <summary>The action kinds a window honors, declared so the harness offers exactly these.</summary>
    public static IReadOnlyList<string> NodeActionKinds { get; } =
        ["tap", "doubleTap", "longPress", "fill", "clear", "press", "pressSequentially", "selectOption", "check", "uncheck", "focus", "hover", "dragTo", "scrollIntoView", "swipe"];

    /// <summary>How many wheel notches one viewport swipe turns: about three quarters of a screen at the usual three lines a notch.</summary>
    private const int SwipeNotches = 6;

    public async Task PerformAsync(WindowSession session, SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(action);

        // The observation root is the screen: a swipe on it scrolls the whole viewport, which is the one action a screen takes.
        if (node.Ref == UiaProjection.RootId)
        {
            if (action is not LocatorAction.Swipe screenSwipe)
            {
                throw WinAppSupport.NotActionable($"the screen root takes swipe only, not {KindOf(action)}; act on a node");
            }

            await SwipeAsync(session, screenSwipe.Direction, cancellationToken).ConfigureAwait(false);
            return;
        }

        var entry = session.Resolve(node);

        // A toggle already in the wanted state sends nothing, as the newest observation saw it; one this session toggled since is read again.
        if (action is LocatorAction.Check or LocatorAction.Uncheck && !session.Toggled(entry.Id) && entry.Node.States.Checked == (action is LocatorAction.Check))
        {
            return;
        }

        try
        {
            await session.CommandAsync($"perform {KindOf(action)}", (driver, token) => RunAsync(session, driver, entry, action, token), cancellationToken).ConfigureAwait(false);
            if (action is LocatorAction.Check or LocatorAction.Uncheck)
            {
                session.MarkToggled(entry.Id);
            }
        }
        catch (EngineException cause) when (action is LocatorAction.Fill { Sensitive: true, Value.Length: > 0 } fill)
        {
            // The message can quote the value in any spelling or cut short. The cause is dropped: it holds the text unredacted.
            throw new EngineException(cause.Code, Redactor.ForValue(fill.Value).RedactFragments(cause.Message), cause.Retryable);
        }
    }

    public async Task PressAsync(WindowSession session, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(key);
        var keys = WindowKeys.ToWinApp(key);
        var window = session.MainWindow;
        await session.CommandAsync(
            $"press {key}",
            async (driver, token) =>
            {
                // Keys go through SendInput, which reaches the foreground window only.
                await driver.ForegroundAsync(window, token).ConfigureAwait(false);
                await driver.SendKeysAsync(window, keys.Text, keys.Verbatim, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        await session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Scrolls the viewport: the largest element of the last observation that scrolls along <paramref name="direction"/>, by the
    /// wheel (up and down) or its scroll pattern, else the wheel over the middle of the main window.
    /// </summary>
    public async Task SwipeAsync(WindowSession session, ScrollDirection direction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var window = session.MainWindow;
        var scrollable = session.Latest
            .Where(entry => entry.WindowHandle == window && UiaProjection.Scrolls(entry.Raw.ScrollDir, direction) && entry.Raw.Bounds is { Width: > 0, Height: > 0 })
            .OrderByDescending(entry => entry.Raw.Bounds!.Value.Width * entry.Raw.Bounds!.Value.Height)
            .FirstOrDefault();
        if (scrollable is not null)
        {
            // The wheel moves a list about a screen at a time; the scroll pattern steps by a share of the whole extent, which a long
            // virtualized list can make too coarse to ever land a row in view. Without a usable desktop the pattern still scrolls.
            if (direction is ScrollDirection.Up or ScrollDirection.Down)
            {
                try
                {
                    await WheelAsync(session, window, WinAppSupport.Centre(scrollable.Raw.Bounds!.Value), direction, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (EngineException cause) when (cause.Code == EngineErrorCodes.NotActionable)
                {
                }
            }

            await session.CommandAsync("swipe", (driver, token) => driver.ScrollAsync(scrollable.Handle, direction, token), cancellationToken).ConfigureAwait(false);
            await session.SettleAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (direction is ScrollDirection.Left or ScrollDirection.Right)
        {
            // The library turns the vertical wheel only, and no key scrolls sideways without a control to take it.
            throw WinAppSupport.NotActionable($"no control on the screen scrolls {direction.ToString().ToLowerInvariant()}; swipe a scrollable node instead");
        }

        var windows = await session.WindowsAsync(cancellationToken).ConfigureAwait(false);
        var main = windows.FirstOrDefault(info => info.Handle == window) ?? throw WinAppSupport.InvalidState("the app window is gone; call app.open()");
        await WheelAsync(session, window, WinAppSupport.Centre(main.Bounds), direction, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Brings the window forward and turns the wheel over a point: up or down by <see cref="SwipeNotches"/> notches.</summary>
    private static async Task WheelAsync(WindowSession session, long window, Point at, ScrollDirection direction, CancellationToken cancellationToken)
    {
        var notches = direction == ScrollDirection.Down ? -SwipeNotches : SwipeNotches;
        await session.CommandAsync(
            "swipe",
            async (driver, token) =>
            {
                await driver.ForegroundAsync(window, token).ConfigureAwait(false);
                await driver.ScrollWheelAsync(at, notches, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        await session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Captures the main window after an observation, so the secure fields it reported are known, and paints each one over before
    /// the image leaves the engine. A secure field without a box withholds the image: the mask could not cover it.
    /// </summary>
    public async Task<EngineScreenshot> ScreenshotAsync(WindowSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        await session.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (session.HasUnmaskableSecureField())
        {
            throw WinAppSupport.Failure("screenshot withheld: a secure field on the screen has no box to cover");
        }

        var window = session.MainWindow;
        var windows = await session.WindowsAsync(cancellationToken).ConfigureAwait(false);
        var main = windows.FirstOrDefault(info => info.Handle == window) ?? throw WinAppSupport.InvalidState("the app window is gone; call app.open()");
        var shot = await session.CommandAsync("screenshot", (driver, token) => driver.ScreenshotAsync(window, token), cancellationToken).ConfigureAwait(false);
        var masks = session.SecureRects().Select(rect => new Rect(rect.X - main.Bounds.X, rect.Y - main.Bounds.Y, rect.Width, rect.Height)).ToList();
        var (png, masked) = WindowScreenshots.Render(shot, masks);
        var scale = main.Bounds.Width > 0 ? shot.Width / (main.Bounds.Width / session.Scale) : session.Scale;
        return new EngineScreenshot(png, scale, masked);
    }

    /// <summary>A hover over the middle of a node.</summary>
    public static async Task HoverAsync(WindowSession session, SemanticNode node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var entry = session.Resolve(node);
        var at = PointOf(entry, "hover");
        await session.CommandAsync(
            "perform hover",
            async (driver, token) =>
            {
                await driver.ForegroundAsync(entry.WindowHandle, token).ConfigureAwait(false);
                await driver.HoverAsync(at, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        await session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A right click on the middle of a node.</summary>
    public static async Task SecondaryTapAsync(WindowSession session, SemanticNode node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var entry = session.Resolve(node);
        var at = PointOf(entry, "secondaryTap");
        await session.CommandAsync(
            "perform secondaryTap",
            async (driver, token) =>
            {
                await driver.ForegroundAsync(entry.WindowHandle, token).ConfigureAwait(false);
                await driver.ClickAsync(at, doubleClick: false, rightClick: true, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        await session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A press held on the middle of a node: a drag that goes nowhere, held for the duration.</summary>
    public static async Task LongPressAsync(WindowSession session, SemanticNode node, TimeSpan? duration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var entry = session.Resolve(node);
        var at = PointOf(entry, "longPress");
        var hold = duration ?? DefaultLongPress;
        await session.CommandAsync(
            "perform longPress",
            async (driver, token) =>
            {
                await driver.ForegroundAsync(entry.WindowHandle, token).ConfigureAwait(false);
                await driver.DragAsync(at, at, hold, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        await session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A drag from the middle of one node to the middle of another.</summary>
    public static async Task DragToAsync(WindowSession session, SemanticNode node, SemanticNode destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var from = session.Resolve(node);
        var to = session.Resolve(destination);
        var start = PointOf(from, "dragTo");
        var end = PointOf(to, "dragTo");
        await session.CommandAsync(
            "perform dragTo",
            async (driver, token) =>
            {
                await driver.ForegroundAsync(from.WindowHandle, token).ConfigureAwait(false);
                await driver.DragAsync(start, end, TimeSpan.Zero, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        await session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunAsync(WindowSession session, IWindowDriver driver, NodeBinding entry, LocatorAction action, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case LocatorAction.Tap:
                await TapAsync(session, driver, entry, doubleClick: false, cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.DoubleTap:
                await TapAsync(session, driver, entry, doubleClick: true, cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Fill fill:
                await FillAsync(session, driver, entry, fill.Value, cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Clear:
                await FillAsync(session, driver, entry, "", cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Press press:
                {
                    var keys = WindowKeys.ToWinApp(press.Key);
                    await driver.FocusAsync(entry.Handle, cancellationToken).ConfigureAwait(false);
                    await driver.ForegroundAsync(entry.WindowHandle, cancellationToken).ConfigureAwait(false);
                    await driver.SendKeysAsync(entry.WindowHandle, keys.Text, keys.Verbatim, cancellationToken).ConfigureAwait(false);
                    await session.SettleAsync(cancellationToken).ConfigureAwait(false);
                    break;
                }

            case LocatorAction.PressSequentially typed:
                await driver.FocusAsync(entry.Handle, cancellationToken).ConfigureAwait(false);
                await driver.ForegroundAsync(entry.WindowHandle, cancellationToken).ConfigureAwait(false);
                await driver.SendKeysAsync(entry.WindowHandle, typed.Text, verbatim: true, cancellationToken).ConfigureAwait(false);
                await session.SettleAsync(cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Select select:
                await SelectAsync(session, driver, entry, select.Value, cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Check:
                await driver.InvokeAsync(entry.Handle, entry.Role == "radio" ? InvokeKind.Select : InvokeKind.ToggleOn, cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Uncheck:
                if (entry.Role == "radio")
                {
                    throw WinAppSupport.NotActionable("a radio button cannot be unchecked; check another one in its group");
                }

                await driver.InvokeAsync(entry.Handle, InvokeKind.ToggleOff, cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Focus:
                await driver.FocusAsync(entry.Handle, cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.ScrollIntoView:
                await driver.ScrollIntoViewAsync(entry.Handle, cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Swipe swipe:
                if (!UiaProjection.Scrolls(entry.Raw.ScrollDir, swipe.Direction))
                {
                    throw WinAppSupport.NotActionable($"the node does not scroll {swipe.Direction.ToString().ToLowerInvariant()}; swipe a list or scroll viewer, or the screen root");
                }

                await driver.ScrollAsync(entry.Handle, swipe.Direction, cancellationToken).ConfigureAwait(false);
                await session.SettleAsync(cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw WinAppSupport.Unsupported($"{KindOf(action)} is not supported by the Windows app engine");
        }
    }

    /// <summary>A tap is the element's own pattern when it has one (a button's invoke, a list item's select, a check box's toggle), else a click on its middle.</summary>
    private static async Task TapAsync(WindowSession session, IWindowDriver driver, NodeBinding entry, bool doubleClick, CancellationToken cancellationToken)
    {
        if (!doubleClick && entry.Raw.IsInvokable && entry.Role is not "textbox" and not "document")
        {
            await driver.InvokeAsync(entry.Handle, InvokeKind.Auto, cancellationToken).ConfigureAwait(false);
            return;
        }

        var at = PointOf(entry, doubleClick ? "doubleTap" : "tap");
        await driver.ForegroundAsync(entry.WindowHandle, cancellationToken).ConfigureAwait(false);
        await driver.ClickAsync(at, doubleClick, rightClick: false, cancellationToken).ConfigureAwait(false);
        await session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A fill is the value pattern when the field takes it, else focus, select all, and type the text, which a rich edit box needs.</summary>
    private static async Task FillAsync(WindowSession session, IWindowDriver driver, NodeBinding entry, string value, CancellationToken cancellationToken)
    {
        try
        {
            await driver.SetValueAsync(entry.Handle, value, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (WindowDriverException cause) when (cause.Fault == DriverFault.ValueRejected)
        {
            // Fall through to the keyboard.
        }

        await driver.FocusAsync(entry.Handle, cancellationToken).ConfigureAwait(false);
        await driver.ForegroundAsync(entry.WindowHandle, cancellationToken).ConfigureAwait(false);
        await driver.SendKeysAsync(entry.WindowHandle, "ctrl+a", verbatim: false, cancellationToken).ConfigureAwait(false);
        if (value.Length == 0)
        {
            await driver.SendKeysAsync(entry.WindowHandle, "delete", verbatim: false, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await driver.SendKeysAsync(entry.WindowHandle, value, verbatim: true, cancellationToken).ConfigureAwait(false);
        }

        await session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Selects an option by its name or value: a combo box is expanded first and read again so its list is in the tree; a list,
    /// tab list, or radio group is read as it is. The option is selected through its pattern.
    /// </summary>
    private static async Task SelectAsync(WindowSession session, IWindowDriver driver, NodeBinding entry, string value, CancellationToken cancellationToken)
    {
        if (entry.Role == "combobox" && !entry.Node.States.Expanded)
        {
            await driver.InvokeAsync(entry.Handle, InvokeKind.Expand, cancellationToken).ConfigureAwait(false);
            await session.SettleAsync(cancellationToken).ConfigureAwait(false);
        }

        // The options may live in a popup window of their own, so the whole app is read again.
        await session.ObserveAsync(cancellationToken).ConfigureAwait(false);
        var container = session.Latest.FirstOrDefault(latest => latest.Handle == entry.Handle);
        var options = session.Latest
            .Where(candidate => candidate.Role is "listitem" or "menuitem" or "tab" or "radio" or "option" or "treeitem")
            .Where(candidate => container is null || candidate.WindowHandle != container.WindowHandle || Contains(container.Node, candidate.Id))
            .ToList();
        var option = options.FirstOrDefault(candidate => Matches(candidate, value))
            ?? throw WinAppSupport.NotActionable($"no option named \"{value}\" is listed under the node; the options listed are {Listed(options)}");
        await driver.InvokeAsync(option.Handle, InvokeKind.Select, cancellationToken).ConfigureAwait(false);
        await session.SettleAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool Matches(NodeBinding option, string value) =>
        string.Equals(option.Node.Name, value, StringComparison.Ordinal)
        || string.Equals(option.Node.Value, value, StringComparison.Ordinal)
        || string.Equals(option.Node.TestId, value, StringComparison.Ordinal)
        || string.Equals(option.Node.Name?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Listed(IReadOnlyList<NodeBinding> options)
    {
        var names = options.Select(option => option.Node.Name).Where(name => !string.IsNullOrEmpty(name)).Distinct().Take(12).Select(name => $"\"{name}\"").ToList();
        return names.Count == 0 ? "none" : string.Join(", ", names);
    }

    private static bool Contains(SemanticNode node, string id) => node.Children.Any(child => child.Ref == id || Contains(child, id));

    /// <summary>The screen point a pointer action lands on: the middle of the element's box. An element without one, or off the screen, cannot take a pointer.</summary>
    private static Point PointOf(NodeBinding entry, string kind)
    {
        if (entry.Raw.IsOffscreen || entry.Raw.Bounds is not { Width: > 0, Height: > 0 } bounds)
        {
            throw WinAppSupport.NotActionable($"the node is off the screen, so {kind} cannot reach it; scroll it into view first");
        }

        return WinAppSupport.Centre(bounds);
    }

    /// <summary>The kind of an action, as the agent and the trace name it.</summary>
    internal static string KindOf(LocatorAction action) => action switch
    {
        LocatorAction.Tap => "tap",
        LocatorAction.DoubleTap => "doubleTap",
        LocatorAction.Fill => "fill",
        LocatorAction.Press => "press",
        LocatorAction.PressSequentially => "pressSequentially",
        LocatorAction.Select => "selectOption",
        LocatorAction.Check => "check",
        LocatorAction.Uncheck => "uncheck",
        LocatorAction.Clear => "clear",
        LocatorAction.Focus => "focus",
        LocatorAction.ScrollIntoView => "scrollIntoView",
        LocatorAction.Swipe => "swipe",
        _ => action.GetType().Name,
    };
}
