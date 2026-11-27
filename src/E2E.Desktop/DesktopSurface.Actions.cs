// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;

namespace E2E.Desktop;

internal sealed partial class DesktopSurface
{
    /// <summary>How far a swipe scrolls, in screens of the scrolled area, by its momentum: the web engine's strides, so a momentum means the same on every engine.</summary>
    private static double SwipePages(Momentum? momentum) =>
        momentum switch
        {
            Momentum.Fast => 1.5,
            Momentum.Slow => 0.75,
            _ => 0.5,
        };

    /// <summary>Roles whose <c>Click</c> may be a selection (<c>AXSelected</c>), not a press of their own.</summary>
    private static readonly HashSet<string> SelectedByClick = ["row", "cell", "treeitem", "option", "tab"];

    /// <summary>The names the driver gives a scroll's direction.</summary>
    private static string DirectionName(ScrollDirection direction) =>
        direction switch
        {
            ScrollDirection.Up => "up",
            ScrollDirection.Down => "down",
            ScrollDirection.Left => "left",
            ScrollDirection.Right => "right",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction."),
        };

    // --- acting ---

    private string RefOf(NodeBinding binding, string action) =>
        binding.Ref
        ?? throw NotActionable(
            $"node {binding.Id} ({binding.Role ?? "unknown"}) is not a control the engine can {action}"
        );

    /// <summary>Upstream's <c>perform</c>.</summary>
    public async Task PerformAsync(string refId, LocatorAction action, DesktopOperation context)
    {
        ArgumentNullException.ThrowIfNull(action);
        var operation = context.WithDeadline();
        var kind = EngineActionKinds.Of(action);
        if (refId == DesktopNodes.RootId)
        {
            // The window is in view unless it is minimized: toHaveScreenshot on a window locator scrolls it into view first.
            if (action is LocatorAction.ScrollIntoView)
            {
                var shown = await CaptureAsync(operation).ConfigureAwait(false);
                if (shown is null || shown.Root.States.Hidden)
                {
                    throw NotActionable(
                        "the window is not on screen (minimized, or closed), so it cannot be brought into view"
                    );
                }

                return;
            }

            if (action is not LocatorAction.Swipe rootSwipe)
            {
                throw NotActionable($"the window takes swipe and scrollIntoView only, not {kind}; act on a node");
            }

            // The root is the window as it is now: read it again, since a locate or a restart may have moved on from the last observation.
            var screen = await CaptureAsync(operation).ConfigureAwait(false);
            if (screen is not null)
            {
                Remember(screen);
            }

            var horizontal = rootSwipe.Direction is ScrollDirection.Left or ScrollDirection.Right;
            var target = horizontal ? screen?.ScrollTargets?.Horizontal : screen?.ScrollTargets?.Vertical;
            if (target is null && screen?.CutShort == true)
            {
                throw PartialRead($"what in it scrolls {(horizontal ? "sideways" : "up and down")}");
            }

            if (target is null)
            {
                throw NotActionable($"the window has nothing that scrolls {(horizontal ? "sideways" : "up and down")}");
            }

            await ScrollAsync(BindingOf(target), rootSwipe.Direction, rootSwipe.Momentum, operation)
                .ConfigureAwait(false);
            await SettleAsync(operation).ConfigureAwait(false);
            return;
        }

        var binding = BindingOf(refId);
        switch (action)
        {
            case LocatorAction.Tap:
            {
                var scrolled = await ShownAsync(binding, "tap", operation).ConfigureAwait(false);
                Task Click() => RefCommandAsync("click", binding, operation);
                if (scrolled)
                {
                    await AfterChangeAsync("click", Click).ConfigureAwait(false);
                }
                else
                {
                    await Click().ConfigureAwait(false);
                }

                break;
            }

            case LocatorAction.SecondaryTap:
                // The driver checks the control offers a context menu, live, before it scrolls one out of view into view (or refuses one it cannot
                // reach), and counts a failure after that scroll as delivered: the observed actions may be from a read that stalled, which proves
                // no menu absent.
                await RefCommandAsync("rightClick", binding, operation).ConfigureAwait(false);
                break;
            case LocatorAction.DoubleTap:
                // A double click needs the real pointer; declared with `headed` alone. A node with no ref (a plain view, text) takes it at its center.
                if (binding.Ref is null)
                {
                    await PointAtNodeAsync(binding, "doubleTap", operation).ConfigureAwait(false);
                    return;
                }

                await RefCommandAsync("doubleClick", binding, operation).ConfigureAwait(false);
                break;
            case LocatorAction.Focus:
                await RefCommandAsync("focus", binding, operation).ConfigureAwait(false);
                break;
            case LocatorAction.Clear:
                await RefCommandAsync("clear", binding, operation).ConfigureAwait(false);
                break;
            case LocatorAction.Check:
                // The driver reads the live state, so a box already right gets nothing; the observed state may be stale. A box it must press while a
                // scroll area holds it out of view is scrolled into view first, or refused, as a tap is.
                await RefCommandAsync("check", binding, operation).ConfigureAwait(false);
                break;
            case LocatorAction.Uncheck:
                await RefCommandAsync("uncheck", binding, operation).ConfigureAwait(false);
                break;
            case LocatorAction.ScrollIntoView:
                // A node outside the window is read offscreen, as hidden, so one not hidden is in view already. It is scrolled only when it can be (a
                // ref and the driver's ScrollTo): most static text has no ref, and a button need not take the scroll action, which an element
                // screenshot asks for before it captures.
                if (!binding.States.Hidden && (binding.Ref is null || !binding.Actions.Contains("ScrollTo")))
                {
                    break;
                }

                await RefCommandAsync("scrollTo", binding, operation).ConfigureAwait(false);
                break;
            case LocatorAction.Hover:
                if (binding.Ref is null)
                {
                    await PointAtNodeAsync(binding, "hover", operation).ConfigureAwait(false);
                    return;
                }

                await RefCommandAsync("hover", binding, operation).ConfigureAwait(false);
                break;
            case LocatorAction.Fill fill:
                await RefCommandAsync("setValue", binding, operation, new ActExtras { Value = fill.Value })
                    .ConfigureAwait(false);
                break;
            case LocatorAction.Press press:
                await PressOnNodeAsync(binding, press.Key, operation).ConfigureAwait(false);
                break;
            case LocatorAction.Select select:
                await SelectAsync(binding, OptionLabel(select), operation).ConfigureAwait(false);
                break;
            case LocatorAction.Swipe swipe:
                await ScrollAsync(binding, swipe.Direction, swipe.Momentum, operation).ConfigureAwait(false);
                break;
            default:
                // Not declared in `actions` (engine.ts), so the harness never sends them; kept exhaustive.
                throw Unsupported($"the desktop engine cannot perform {kind}");
        }

        await SettleAsync(operation).ConfigureAwait(false);
    }

    /// <summary>The <c>press</c> of a locator: the key goes to the control's own window, with the checks that it kept focus.</summary>
    private async Task PressOnNodeAsync(NodeBinding binding, string key, DesktopOperation operation)
    {
        // A key the engine cannot press is refused before focus moves, and so is one this Mac's keyboard layout cannot type (a digit shortcut on AZERTY).
        var combo = DesktopKeys.ToCombo(key);
        var window = binding.WindowId
            ?? throw NotActionable($"node {binding.Id} was read in no window to press a key into");
        try
        {
            await ReadAsync(
                    (driver, _) => driver.CheckComboAsync(combo),
                    new DriverCall
                    {
                        Label = "press",
                        Input = false,
                        Signal = operation.Signal,
                        TimeoutMs = operation.TimeoutMs,
                    }
                )
                .ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw DesktopDriverCalls.Translate(cause, "press", false);
        }

        // With `headed` the key goes through the real keyboard to the window in front: the control's window comes forward first, so a control in a
        // window useWindow() picked while another held the keyboard can take focus. The checks below still run before the key.
        if (Options.Headed && _pid is { } pid)
        {
            var focusId = RequireDriver().InputId();
            try
            {
                await InputAsync(
                        (driver, timeoutMs) => driver.FocusWindowAsync(pid, window, focusId, timeoutMs),
                        new DriverCall
                        {
                            Label = "focus window",
                            Input = true,
                            Signal = operation.Signal,
                            TimeoutMs = operation.TimeoutMs,
                            OnCutOff = () => _driver?.CancelInput(focusId),
                        }
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception cause) when (cause is not OperationCanceledException)
            {
                throw DesktopDriverCalls.Translate(cause, "focus window", true);
            }
        }

        await RefCommandAsync("focus", binding, operation).ConfigureAwait(false);
        try
        {
            // The key goes to the process, which hands it to its key window: with another window that could be it, the control's must be the one
            // shown focused. A window on another Space, which the window list shows as not visible, is still the control's: with `headed` the press
            // brings it forward.
            var windows = await OwnWindowsAsync(operation).ConfigureAwait(false);
            var home = windows.FirstOrDefault(candidate => candidate.Id == window)
                ?? throw NotActionable($"the window node {binding.Id} was read in is gone");
            if (home.Minimized == true)
            {
                throw NotActionable($"the window node {binding.Id} was read in is minimized, so no key reaches it");
            }

            var rivals = windows.Where(candidate =>
                candidate.Id != window && candidate.Visible != false && candidate.Minimized != true
            );
            if (rivals.Any() && home.IsFocused != true)
            {
                throw NotActionable(
                    $"node {binding.Id} is in a window that cannot be shown to be the app's key window, so the key could land in another window"
                );
            }

            // Focusing can hand focus on (a dialog it opens): the key goes only when the control still holds it.
            if (!await HoldsFocusAsync(binding, window, operation).ConfigureAwait(false))
            {
                throw NotActionable(
                    $"node {binding.Id} did not keep keyboard focus, so the key would land on another control"
                );
            }

            await SendKeyAsync(combo, window, $"press {key}", operation, binding.Ref).ConfigureAwait(false);
        }
        catch (EngineException cause) when (cause.Retryable)
        {
            // The focus already moved: a retry of the whole press would focus the control a second time, which an app's focus handler sees.
            throw new EngineException(
                "ACTION_MAY_HAVE_COMMITTED",
                $"press {key}: {cause.Message} (the control was already focused)",
                retryable: false,
                cause
            );
        }
    }

    /// <summary>The agent reads the screen right after acting, so its actions let the UI settle first; a test's own steps verify with <c>expect</c>.</summary>
    private async Task SettleAsync(DesktopOperation operation)
    {
        if (operation.Origin == OperationOrigin.Agent)
        {
            await LaunchOwnership
                .SleepAsync(Math.Min(Options.SettleMs, operation.TimeoutMs), operation.Signal)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Refuses a tap on a node the read found off screen, which accessibility would press all the same: one a scroll area holds out of view. When
    /// the driver can scroll it into view, it does, and answers only once the element shows: a fresh read could not tell a look-alike node that
    /// moved from its siblings.
    /// </summary>
    private async Task<bool> ShownAsync(NodeBinding binding, string what, DesktopOperation operation)
    {
        if (!binding.States.Hidden)
        {
            return false;
        }

        // Only a node a scroll area holds out of view is scrolled: the driver checks it shows by its frame and the scroll areas around it, which a
        // minimized window or a collapsed control would pass unchanged.
        if (binding.ScrolledOut && binding.Ref is not null && binding.Actions.Contains("ScrollTo"))
        {
            await RefCommandAsync("scrollTo", binding, operation).ConfigureAwait(false);
            return true;
        }

        throw NotActionable(
            $"node {binding.Id} is not on screen, so a {what} would reach a control the user cannot; scroll it into view first"
        );
    }

    /// <summary>The extra arguments of <see cref="IDesktopDriver.ActAsync"/>.</summary>
    private sealed record ActExtras
    {
        public string? Value { get; init; }

        public string? Direction { get; init; }

        public double? Pages { get; init; }

        public bool? Physical { get; init; }
    }

    /// <summary>One action on a node's ref, failures translated.</summary>
    private async Task RefCommandAsync(
        string action,
        NodeBinding binding,
        DesktopOperation operation,
        ActExtras? extra = null
    )
    {
        try
        {
            await TryRefAsync(action, binding, operation, extra).ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw DesktopDriverCalls.Translate(cause, action, true);
        }
    }

    /// <summary>One action on a node's ref, a failure left as the driver reported it so a caller can tell what was sent.</summary>
    private async Task TryRefAsync(
        string action,
        NodeBinding binding,
        DesktopOperation operation,
        ActExtras? extra = null
    )
    {
        var reference = RefOf(binding, action);
        // Input still to send once the call is given up is never sent: the app may be another's by then, or the step over.
        var inputId = RequireDriver().InputId();
        _ = await InputAsync(
                (driver, timeoutMs) =>
                    driver.ActAsync(
                        new ActRequest
                        {
                            Ref = reference,
                            Action = action,
                            InputId = inputId,
                            TimeoutMs = timeoutMs,
                            Value = extra?.Value,
                            Direction = extra?.Direction,
                            Pages = extra?.Pages,
                            Physical = extra?.Physical,
                        }
                    ),
                new DriverCall
                {
                    Label = action,
                    Input = true,
                    Signal = operation.Signal,
                    TimeoutMs = operation.TimeoutMs,
                    OnCutOff = () => _driver?.CancelInput(inputId),
                }
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the control a binding names holds keyboard focus in a fresh read of its window. A control with an identity keeps its id; a look-alike
    /// has a new id in every read, so it is found by role, name, developer id, and place, with as many twins around it as before: a twin that went
    /// could have let another take its place.
    /// </summary>
    private async Task<bool> HoldsFocusAsync(NodeBinding binding, string window, DesktopOperation operation)
    {
        // Read as the binding was: a locator's binding comes from a whole read, an observation's from an interactive one.
        var now = await CaptureAsync(operation, window, forMask: false, whole: !binding.Interactive)
            .ConfigureAwait(false);
        // A read that ran out of time gives every control a fresh id, so it cannot tell which one holds focus: that is no lost focus.
        if (now?.CutShort == true)
        {
            throw PartialRead("which control holds focus");
        }

        return (now?.Bindings.Values ?? Enumerable.Empty<NodeBinding>()).Any(candidate =>
            candidate.States.Focused && SameNode(candidate, binding)
        );
    }

    /// <summary>
    /// Chooses an option of a list or dropdown, never by setting the value, which would put a missing option in an editable combo box as free text:
    /// one is refused. For a pop-up button the driver opens its menu, chooses the item, checks the button shows it, and closes the menu again when
    /// the option is missing. A stale ref is <c>NODE_STALE</c>, so the harness locates the list again. A failure after the menu opened stays one
    /// that may have chosen something; a menu the driver could not close is named, since it covers the window for the steps after.
    /// </summary>
    private async Task SelectAsync(NodeBinding binding, string label, DesktopOperation operation)
    {
        // An editable combo box takes any text: a missing option would land in it as free text.
        if (binding.Role == "combobox" && binding.Actions.Contains("SetValue"))
        {
            throw Unsupported(
                $"this combo box is editable and takes any text, so a select of \"{label}\" could not tell a missing option from a chosen one; fill() it and check its value"
            );
        }

        try
        {
            await TryRefAsync("select", binding, operation, new ActExtras { Value = label }).ConfigureAwait(false);
        }
        catch (DriverFailure cause) when (cause is { NotDelivered: true, Code: "ELEMENT_NOT_FOUND" })
        {
            throw NotActionable($"the list has no option \"{label}\"");
        }
        catch (DriverFailure cause) when (!cause.NotDelivered && DetailIs(cause, "menu", "open"))
        {
            throw new EngineException(
                "ACTION_MAY_HAVE_COMMITTED",
                $"select \"{label}\": {cause.Message}. The list's menu is still open",
                retryable: false,
                cause
            );
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw DesktopDriverCalls.Translate(cause, $"select \"{label}\"", true);
        }
    }

    /// <summary>The label a select names: a bare string, or <c>{ label }</c>; a <c>value</c> is a desktop option's label too, since a native list has no other value.</summary>
    private static string OptionLabel(LocatorAction.Select select)
    {
        if (select.Text is { } text)
        {
            return text;
        }

        var option = select.Option;
        if (option?.Label is { } label)
        {
            return label;
        }

        return option?.Value
            ?? throw Unsupported("a desktop list selects an option by its label; select by { index } is not supported");
    }

    private Task ScrollAsync(
        NodeBinding binding,
        ScrollDirection direction,
        Momentum? momentum,
        DesktopOperation operation
    ) =>
        RefCommandAsync(
            "scroll",
            binding,
            operation,
            new ActExtras { Direction = DirectionName(direction), Pages = SwipePages(momentum) }
        );

    /// <summary>
    /// A pointer action at the center of a node the driver gave no ref, through the checks a point action makes: the node's window forward, the
    /// point inside it.
    /// </summary>
    private async Task PointAtNodeAsync(NodeBinding binding, string kind, DesktopOperation operation)
    {
        var what = kind == "hover" ? "hover" : "double-click";
        if (binding.WindowId is null)
        {
            throw NotActionable($"node {binding.Id} ({binding.Role ?? "unknown"}) has no box on screen to {what}");
        }

        if (binding.States.Hidden)
        {
            throw NotActionable($"node {binding.Id} is not on screen; scroll it into view first");
        }

        // With no ref, the driver cannot check the node is still there: the pointer goes where a fresh read of its window finds it now, or nowhere.
        // The box it was read with could hold what replaced it, and a node with a stable id may have moved.
        var now = await CaptureAsync(operation, binding.WindowId, forMask: false, whole: !binding.Interactive)
            .ConfigureAwait(false);
        if (now?.CutShort == true)
        {
            throw PartialRead($"whether node {binding.Id} is still on screen");
        }

        // A read stopped at the depth limit left out what is below some nodes: a control at the center may be one of them.
        if (kind == "doubleTap" && now?.Truncated == true)
        {
            throw new EngineException(
                "NODE_STALE",
                $"the window was read only in part (it is deeper than the {Options.MaxDepth} levels a snapshot reads, which desktop({{ maxDepth }}) raises, or holds more than the {MaxSnapshotNodes} elements it reads), so what is at the center of node {binding.Id} cannot be told",
                retryable: true
            );
        }

        var fresh = now?.Bindings.Values.FirstOrDefault(candidate => SameNode(candidate, binding))
            ?? throw new EngineException(
                "NODE_STALE",
                $"node {binding.Id} ({binding.Role ?? "unknown"}) is no longer on screen; observe again",
                retryable: true
            );
        if (fresh.States.Hidden)
        {
            throw NotActionable($"node {binding.Id} is not on screen; scroll it into view first");
        }

        if (fresh.Rect is not { } rect || rect.Width <= 0 || rect.Height <= 0)
        {
            throw NotActionable($"node {binding.Id} ({binding.Role ?? "unknown"}) has no box on screen to {what}");
        }

        var center = new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        // A point names no element: the driver's check takes any control of the window there. Any other control at this node's center (a button
        // inside it, even one reaching past its box, or one laid over it) would take the double-click, so it is refused here, from the same read.
        // The node's ancestors hold the point by being around it, and only one that takes a press of its own (a button the text is in) takes the
        // click, as the driver judges it. A hover presses nothing.
        if (kind == "doubleTap")
        {
            var around = new HashSet<string>(now is null ? [] : Ancestors(now.Root, fresh.Id), StringComparer.Ordinal);
            var other = now!.Bindings.Values.FirstOrDefault(candidate =>
                candidate.Id != fresh.Id
                && (!around.Contains(candidate.Id) || Pressable(candidate))
                && candidate.Ref is not null
                && !candidate.States.Hidden
                && candidate.Rect is { } candidateRect
                && Holds(candidateRect, center)
            );
            if (other is not null)
            {
                throw NotActionable(
                    $"a {other.Role ?? "control"} is at the center of node {binding.Id}, so a double-click there would press it; act on that control instead"
                );
            }
        }

        _pointWindow = new PointWindow(binding.WindowId, now?.Sheet);
        // Coming forward can reveal a control over the node: the driver checks after the move that none is under a double-click.
        await PointerAtAsync(center, kind, operation, content: kind == "doubleTap").ConfigureAwait(false);
    }

    /// <summary>Physical pointer input at a point in the window: only with <c>headed</c>, and only then declared. Upstream's <c>performAt</c>.</summary>
    public Task PerformAtAsync(Point point, PointerAction action, DesktopOperation context)
    {
        ArgumentNullException.ThrowIfNull(action);
        var kind = action switch
        {
            PointerAction.Tap => "tap",
            PointerAction.DoubleTap => "doubleTap",
            PointerAction.SecondaryTap => "secondaryTap",
            PointerAction.Hover => "hover",
            _ => throw Unsupported($"the desktop engine cannot perform {EngineActionKinds.Of(action)} at a point"),
        };
        return PointerAtAsync(point, kind, context, content: false);
    }

    /// <summary>
    /// <see cref="PerformAtAsync"/>, and <paramref name="content"/> when the point is on a node with no ref, which takes no click of its own: the
    /// driver then refuses a control under the pointer after the move, not only another window.
    /// </summary>
    private async Task PointerAtAsync(Point point, string kind, DesktopOperation context, bool content)
    {
        var operation = context.WithDeadline();
        // The point is in the observed window's coordinates: find that window where it is now (it may have moved), or the app's own window before any observation.
        var observed = _pointWindow;
        var target = observed is null
            ? await OwnWindowAsync(operation).ConfigureAwait(false)
            : (await OwnWindowsAsync(operation).ConfigureAwait(false)).FirstOrDefault(candidate =>
                candidate.Id == observed.Id
            );
        if (target is null || target.Bounds is not { } window)
        {
            throw NotActionable("the window the point was taken from is no longer on screen; observe again");
        }

        if (point.X < 0 || point.Y < 0 || point.X >= window.Width || point.Y >= window.Height)
        {
            throw NotActionable(
                $"({Number(point.X)}, {Number(point.Y)}) is outside the {Number(window.Width)}x{Number(window.Height)} window; a click there would land on another app"
            );
        }

        var pid = _pid ?? throw InvalidState("the app is not running; open it with app.open()");
        // Physical input lands on whatever is frontmost at the point: bring the window forward first. Finding the window can wait on the app: a call
        // given up on must not bring it forward later, over what the user or the next step is doing.
        var focusId = RequireDriver().InputId();
        try
        {
            await InputAsync(
                    (driver, timeoutMs) => driver.FocusWindowAsync(pid, target.Id, focusId, timeoutMs),
                    new DriverCall
                    {
                        Label = "focus window",
                        Input = true,
                        Signal = operation.Signal,
                        TimeoutMs = operation.TimeoutMs,
                        OnCutOff = () => _driver?.CancelInput(focusId),
                    }
                )
                .ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw DesktopDriverCalls.Translate(cause, "focus window", true);
        }

        // Coming forward can move the window (onto another space, out from under the menu bar): the click is measured from where it is now.
        var forward = (await OwnWindowsAsync(operation).ConfigureAwait(false)).FirstOrDefault(candidate =>
            candidate.Id == target.Id
        );
        if (forward?.Bounds is not { } focused)
        {
            throw NotActionable("the window the point was taken from closed as it was brought forward; observe again");
        }

        // A click carries no window, only screen coordinates: if another window (a sheet, a panel the app raised) took focus as this one came
        // forward, it could be the one under the point.
        if (forward.IsFocused != true)
        {
            throw NotActionable(
                "another window took focus as the window the point was taken from was brought forward, and could be under the point; observe again"
            );
        }

        // A sheet counts as its window's focus, for keys. The point was taken on the sheet there then (in the observation, or as this action began
        // when none came first): one that opened since could cover it, one that closed uncovered what was under it, and one in its place is another.
        var sheetThen = observed is null ? SheetKey(target) : observed.Sheet;
        var sheetNow = SheetKey(forward);
        if (!SameSheet(sheetNow, sheetThen))
        {
            var what =
                sheetThen is null ? "a sheet opened over the window"
                : sheetNow is null ? "the sheet the point was taken on closed"
                : "another sheet took the place of the one the point was taken on";
            throw NotActionable(
                $"{what} since the point was taken, and the click could land on something else; observe again"
            );
        }

        if (point.X >= focused.Width || point.Y >= focused.Height)
        {
            throw NotActionable(
                $"({Number(point.X)}, {Number(point.Y)}) is outside the {Number(focused.Width)}x{Number(focused.Height)} window; a click there would land on another app"
            );
        }

        // Unrounded: the driver takes fractional points, and rounding could carry a point past the window's edge. As for `TryRefAsync`: pointer
        // input still unsent once the call is given up is never sent.
        var inputId = RequireDriver().InputId();
        // The driver checks the window again after the move, before the click: a sheet or window that opens meanwhile would take it.
        var takes = new PointerTarget(
            pid,
            target.Id,
            sheetNow is not null,
            sheetNow is null || sheetNow == UnknownSheet ? null : sheetNow,
            content ? true : null
        );
        try
        {
            await InputAsync(
                    (driver, timeoutMs) =>
                        driver.PointerAsync(focused.X + point.X, focused.Y + point.Y, kind, inputId, timeoutMs, takes),
                    new DriverCall
                    {
                        Label = kind,
                        Input = true,
                        Signal = operation.Signal,
                        TimeoutMs = operation.TimeoutMs,
                        OnCutOff = () => _driver?.CancelInput(inputId),
                    }
                )
                .ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw DesktopDriverCalls.Translate(cause, kind, true);
        }

        await SettleAsync(operation).ConfigureAwait(false);
    }

    private static string Number(double value) => value.ToString("0.###############", CultureInfo.InvariantCulture);

    /// <summary>The keyboard capability's <c>type</c>: keystrokes into the focused field, which a fresh snapshot names. Upstream's <c>typeText</c>.</summary>
    public async Task TypeTextAsync(string text, bool replace, DesktopOperation context)
    {
        ArgumentNullException.ThrowIfNull(text);
        // Refused before anything is sent: with replace, the select-all and delete would already have emptied the field.
        if (text.Contains('\0', StringComparison.Ordinal))
        {
            throw NotActionable("a NUL character cannot be typed");
        }

        var operation = context.WithDeadline();
        var screen = await CaptureAsync(operation).ConfigureAwait(false);
        var focused = screen?.Bindings.Values.FirstOrDefault(binding =>
            binding.Ref is not null && binding.States.Focused
        );
        // A read that ran out of time may stop short of the focused field: no proof that none has focus.
        if (focused is null && screen?.CutShort == true)
        {
            throw PartialRead("which field has keyboard focus");
        }

        if (focused is null)
        {
            throw NotActionable("no field has keyboard focus; tap or focus the field first");
        }

        // Text goes only into a control that takes it; a list or button with focus would take the select-all and delete as commands.
        if (!focused.Actions.Contains("TypeText"))
        {
            throw NotActionable(
                $"the {focused.Role ?? "control"}{(focused.Name is null ? "" : $" \"{focused.Name}\"")} that has keyboard focus takes no text; tap or focus a text field first"
            );
        }

        // Keystrokes, so the app's key handlers run; with `headed` they go through the real keyboard.
        Task Type() =>
            TryRefAsync("type", focused, operation, new ActExtras { Value = text, Physical = Options.Headed });
        if (replace)
        {
            // Cleared as on a keyboard, select all and then delete: the app's key handlers see the delete, and a field that takes keys but refuses
            // an accessibility write is cleared too.
            var window = focused.WindowId ?? (await OwnWindowAsync(operation).ConfigureAwait(false))?.Id;
            if (window is null)
            {
                throw _chosenWindow is null
                    ? InvalidState("the app is not running; open it with app.open()")
                    : ChosenClosed();
            }

            await SelectAllAsync(focused, window, operation).ConfigureAwait(false);
            // The app may move focus on select all: the delete goes only to the field that still holds it.
            await AfterChangeAsync("delete the selection", async () =>
                {
                    if (!await HoldsFocusAsync(focused, window, operation).ConfigureAwait(false))
                    {
                        throw NotActionable("the field lost keyboard focus after select all, so no delete was sent");
                    }

                    await SendKeyAsync("backspace", window, "delete the selection", operation, focused.Ref)
                        .ConfigureAwait(false);
                })
                .ConfigureAwait(false);
            // The delete's handler may move focus on (an emptied field that advances): the driver would focus the field back to type, taking focus
            // from the app, so the field must still hold it.
            await AfterChangeAsync("type", async () =>
                {
                    if (!await HoldsFocusAsync(focused, window, operation).ConfigureAwait(false))
                    {
                        throw NotActionable("the field lost keyboard focus after the delete, so nothing was typed");
                    }

                    await Type().ConfigureAwait(false);
                })
                .ConfigureAwait(false);
        }
        else
        {
            try
            {
                await Type().ConfigureAwait(false);
            }
            catch (Exception cause) when (cause is not OperationCanceledException)
            {
                throw DesktopDriverCalls.Translate(cause, "type", true);
            }
        }

        await SettleAsync(operation).ConfigureAwait(false);
    }

    /// <summary>
    /// Selects all the text of the focused field. Through accessibility first: Cmd+A sent to an app in the background goes to its menu bar, not the
    /// field, and selects nothing, so the delete after it would remove one character. A field whose selection cannot be set gets the key only with
    /// <c>headed</c>, through the real keyboard; without it, nothing is sent.
    /// </summary>
    private async Task SelectAllAsync(NodeBinding focused, string window, DesktopOperation operation)
    {
        try
        {
            await TryRefAsync("selectAll", focused, operation).ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            if (cause is not DriverFailure { NotDelivered: true, Code: "ACTION_NOT_SUPPORTED" })
            {
                throw DesktopDriverCalls.Translate(cause, "select all", true);
            }

            if (!Options.Headed)
            {
                throw NotActionable(
                    "the focused field's text cannot be selected through accessibility, and Cmd+A sent to an app in the background selects nothing; use fill(), or run headed"
                );
            }

            await SendKeyAsync("cmd+a", window, "select all", operation, focused.Ref).ConfigureAwait(false);
        }
    }

    /// <summary>One key to whatever holds focus in the app's window. Upstream's <c>pressKey</c>.</summary>
    public async Task PressKeyAsync(string key, DesktopOperation context)
    {
        var operation = context.WithDeadline();
        var combo = DesktopKeys.ToCombo(key);
        var window = await OwnWindowAsync(operation).ConfigureAwait(false)
            ?? throw (
                _chosenWindow is null ? InvalidState("the app is not running; open it with app.open()") : ChosenClosed()
            );
        await SendKeyAsync(combo, window.Id, $"press {key}", operation, null).ConfigureAwait(false);
        // As after every other agent action: the app reacts to the key before the agent reads the screen again.
        await SettleAsync(operation).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends one key combo to the launched process's window; with <paramref name="focusedRef"/>, only while that control still holds the app's
    /// focus, checked by the driver right before the key goes. Without <c>headed</c> the key goes into the process's event queue, which hands it to
    /// its key window, so the driver sends it only while the window named is that one. With <c>headed</c>, the driver brings the window forward and
    /// presses the key on the real keyboard.
    /// </summary>
    private async Task SendKeyAsync(
        string combo,
        string window,
        string label,
        DesktopOperation operation,
        string? focusedRef
    )
    {
        var pid = _pid ?? throw InvalidState("the app is not running; open it with app.open()");
        var physical = Options.Headed;
        // As for `TryRefAsync`: a key still unsent once the call is given up is never sent.
        var inputId = RequireDriver().InputId();
        try
        {
            await InputAsync(
                    (driver, timeoutMs) =>
                        driver.PressAsync(
                            new PressRequest
                            {
                                Pid = pid,
                                WindowId = window,
                                Combo = combo,
                                Physical = physical,
                                InputId = inputId,
                                TimeoutMs = timeoutMs,
                                FocusedRef = focusedRef,
                            }
                        ),
                    new DriverCall
                    {
                        Label = label,
                        Input = true,
                        Signal = operation.Signal,
                        TimeoutMs = operation.TimeoutMs,
                        OnCutOff = () => _driver?.CancelInput(inputId),
                    }
                )
                .ConfigureAwait(false);
        }
        catch (DriverFailure cause)
            when (cause.NotDelivered && DetailIs(cause, "cause", "exact_window_focus_unconfirmed"))
        {
            throw NotActionable(
                $"{label}: the window is not the app's key window, so a key sent to the app would land in another; nothing was sent"
            );
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw DesktopDriverCalls.Translate(cause, label, true);
        }
    }

    // --- pixels ---

    /// <summary>Upstream's <c>screenshot</c>: a redacted screenshot artifact; its artifact-relative path.</summary>
    public async Task<string> ScreenshotAsync(string? label, DesktopOperation context)
    {
        var operation = context.WithDeadline();
        var attempt = _attempt ?? throw InvalidState("screenshot outside an attempt");
        var screen = await CaptureAsync(operation).ConfigureAwait(false)
            ?? throw InvalidState("the app has no window to capture; open it with app.open()");
        var (pixels, _) = await PixelsForAsync(screen, operation).ConfigureAwait(false);
        var relative = Path.Combine(
            "screenshots",
            $"{(++attempt.Screenshots).ToString("D3", CultureInfo.InvariantCulture)}-{SanitizeFilename(label ?? "screenshot")}.png"
        );
        Directory.CreateDirectory(Path.Combine(attempt.ArtifactsDir, "screenshots"));
        var absolute = Path.Combine(attempt.ArtifactsDir, relative);
        var temporary = $"{absolute}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, pixels.Png, operation.Signal).ConfigureAwait(false);
            File.Move(temporary, absolute, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }

        return relative.Replace('\\', '/');
    }

    /// <summary>The masked window capture of the screen now, for <c>toHaveScreenshot</c>. Upstream gets it from <c>observe</c> with pixels.</summary>
    public async Task<EngineScreenshot> CaptureScreenshotAsync(DesktopOperation context)
    {
        var operation = context.WithDeadline();
        var screen = await CaptureAsync(operation).ConfigureAwait(false)
            ?? throw InvalidState("the app has no window to capture; open it with app.open()");
        return (await PixelsForAsync(screen, operation).ConfigureAwait(false)).Pixels;
    }

    /// <summary>Constrains a caller-supplied artifact label to a safe filename.</summary>
    private static string SanitizeFilename(string name)
    {
        var safe = new string([
            .. name.Select(character =>
                char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '_'
            ),
        ]);
        safe = safe.Length > 64 ? safe[..64] : safe;
        return safe.Length == 0 ? "artifact" : safe;
    }

    /// <summary>
    /// The window's pixels with every secure field painted over. The fields come from the snapshot taken just before, and the window is read again
    /// after the capture: a secure field without bounds, a partial snapshot, or a field that moved in between withholds the image rather than ship
    /// it unproven. That is a refusal of policy, <c>POLICY_DENIED</c>, as on the mobile engine: the image could hold a credential. A window with no
    /// secure field that closed, changed size, or was read in part because the read ran out of time is a capture that failed, and the next one may work.
    /// </summary>
    private async Task<(EngineScreenshot Pixels, int Masked)> PixelsForAsync(
        ProjectedScreen observed,
        DesktopOperation operation
    )
    {
        if (observed.WindowId is null)
        {
            throw NotActionable("the observed surface has no window to capture");
        }

        ConfigurationException Unmaskable(string reason) =>
            new("POLICY_DENIED", $"{reason}, so the screenshot is withheld", Unmasked);
        // The secure fields come from a whole read of the window, never an interactive one, which can leave a secure node out.
        var screen = Options.InteractiveOnly
            ? await CaptureAsync(operation, observed.WindowId, forMask: true).ConfigureAwait(false)
            : observed;
        if (screen?.WindowId is not { } windowId)
        {
            throw new EngineException(
                "ENGINE_FAILURE",
                "the window closed before it could be captured",
                retryable: false
            );
        }

        // The image goes out beside the observed tree: a window that changed size since that read would pair it with the wrong viewport and boxes.
        if (screen.ViewportWidth != observed.ViewportWidth || screen.ViewportHeight != observed.ViewportHeight)
        {
            throw new EngineException(
                "ENGINE_FAILURE",
                "the window changed size between the read and the capture",
                retryable: false
            );
        }

        var secure = screen.SecureRects ?? throw Unmaskable("a secure field on screen has no bounds to mask");
        // A snapshot cut short can leave out a secure field, which would then go unmasked.
        if (screen.Truncated)
        {
            // One that ran out of time and saw no secure field may read whole next time: a capture that failed, not a refusal.
            if (screen.CutShort && secure.Count == 0)
            {
                throw new EngineException(
                    "ENGINE_FAILURE",
                    "the window was read only in part, as the read ran out of time, so it cannot be shown to hold no secure field",
                    retryable: false
                );
            }

            throw Unmaskable(
                screen.CutShort
                    ? "the snapshot did not read the whole window, so a secure field may be missing from it"
                    : $"the window is deeper than desktop({{ maxDepth }}) reads ({Options.MaxDepth} levels), or holds more than the {MaxSnapshotNodes} elements a snapshot reads, so a secure field may be missing from the read"
            );
        }

        // The capture lands unmasked on disk first: in the worker's private directory, removed below.
        var directory = Directory
            .CreateDirectory(Path.Combine(_home ?? Path.GetTempPath(), "shot-" + Guid.NewGuid().ToString("N")))
            .FullName;
        try
        {
            var file = Path.Combine(directory, "window.png");
            ScreenshotResult shot;
            try
            {
                shot = await ReadAsync(
                        (driver, timeoutMs) => driver.ScreenshotAsync(new ScreenshotRequest(windowId, file, timeoutMs)),
                        new DriverCall
                        {
                            Label = "screenshot",
                            Input = false,
                            Signal = operation.Signal,
                            TimeoutMs = operation.TimeoutMs,
                        }
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception cause) when (cause is not OperationCanceledException)
            {
                throw DesktopDriverCalls.Translate(cause, "screenshot", false);
            }

            // The snapshot and the capture are two calls: read the window again, and withhold the image if a secure field moved, came, or went between them.
            ProjectedScreen? after;
            try
            {
                after = await CaptureAsync(operation, windowId, forMask: true).ConfigureAwait(false);
            }
            catch (EngineException cause) when (cause.Code is "OPERATION_TIMEOUT" or "CANCELLED")
            {
                throw;
            }
            catch (Exception) when (secure.Count > 0)
            {
                // The image holds a secure field the read after it could not place: that is masking not proven, not a capture that failed.
                throw Unmaskable(
                    "the window could not be read again after the capture, so its secure fields cannot be placed"
                );
            }

            if (
                after is null
                || after.WindowId != windowId
                || after.Truncated
                || !SameRects(after.SecureRects, secure)
                || after.ViewportWidth != screen.ViewportWidth
                || after.ViewportHeight != screen.ViewportHeight
            )
            {
                // Only a secure field, seen in either read or possibly below the depth a read stops at, makes this a matter of masking; a read that
                // ran out of time is tried again.
                if (
                    secure.Count > 0
                    || (after is not null && ((after.Truncated && !after.CutShort) || after.SecureRects?.Count != 0))
                )
                {
                    throw Unmaskable("the window changed while it was captured, so its secure fields cannot be placed");
                }

                throw new EngineException(
                    "ENGINE_FAILURE",
                    "the window closed or changed size while it was captured",
                    retryable: false
                );
            }

            var scale = shot.ScaleFactor is > 0 ? shot.ScaleFactor.Value : 1;
            var raw = await File.ReadAllBytesAsync(file, operation.Signal).ConfigureAwait(false);
            var expectedWidth = (int)Math.Round(screen.ViewportWidth * scale, MidpointRounding.AwayFromZero);
            var expectedHeight = (int)Math.Round(screen.ViewportHeight * scale, MidpointRounding.AwayFromZero);
            var rects = secure
                .Select(rect => new Rect(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale))
                .ToList();
            var image = DesktopPng.WindowImage(raw, expectedWidth, expectedHeight, rects);
            if (image is null)
            {
                var mismatch =
                    $"the window capture ({DesktopPng.DescribeCapture(raw)}) does not match the window's {expectedWidth}x{expectedHeight} pixels";
                if (rects.Count > 0)
                {
                    throw Unmaskable($"{mismatch}, so its secure fields cannot be placed");
                }

                throw new EngineException("ENGINE_FAILURE", mismatch, retryable: false);
            }

            return (new EngineScreenshot(DesktopPng.EncodePng(image), scale, rects.Count), rects.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // --- plumbing ---

    private IDesktopDriver RequireDriver() => _driver ?? throw InvalidState("the desktop engine is not initialized");

    /// <summary>A driver call that reads: refused once the budget is spent, its failure left untranslated for the caller.</summary>
    private Task<T> ReadAsync<T>(Func<IDesktopDriver, int, Task<Envelope<T>>> start, DriverCall options) =>
        DriverCallAsync(start, options);

    /// <summary>A driver call that sends input: refused once the budget is spent, its failure left untranslated for the caller.</summary>
    private Task<T> InputAsync<T>(Func<IDesktopDriver, int, Task<Envelope<T>>> start, DriverCall options) =>
        DriverCallAsync(start, options);

    private async Task<T> DriverCallAsync<T>(Func<IDesktopDriver, int, Task<Envelope<T>>> start, DriverCall options)
    {
        var driver = RequireDriver();
        // Nothing is sent once the operation's budget is spent: a later step of an action never starts past the deadline.
        if (options.TimeoutMs <= 0)
        {
            throw new EngineException(
                "OPERATION_TIMEOUT",
                $"{options.Label}: the operation's budget ran out before it was sent",
                retryable: false,
                NotSent
            );
        }

        // A read the driver stops at its deadline answers with what it read, marked incomplete: it must answer before the call is cut off.
        var driverMs = options.Input
            ? options.TimeoutMs
            : (int)Math.Max(options.TimeoutMs - ReadAnswerMarginMs, options.TimeoutMs / 2.0);
        return await DesktopDriverCalls.CallAsync(() => start(driver, driverMs), options).ConfigureAwait(false);
    }

    // --- errors and small helpers ---

    /// <summary>A read that ran out of time, which proves nothing absent: a retryable <c>NODE_STALE</c> that says what it could not tell.</summary>
    private static EngineException PartialRead(string what) =>
        new(
            "NODE_STALE",
            $"the window was read only in part, as the read ran out of time, so {what} cannot be told",
            retryable: true,
            Unreadable
        );

    private static EngineException NotActionable(string text) => new("NOT_ACTIONABLE", text, retryable: false);

    private static EngineException Unsupported(string text) => new("UNSUPPORTED_CAPABILITY", text, retryable: false);

    private static EngineException InvalidState(string text) => new("INVALID_STATE", text, retryable: false);

    /// <summary>
    /// Whether a node of a fresh read is the one a binding names: by its id when that comes from its identity, else by its role, name, developer
    /// id, place, and as many twins around it as before.
    /// </summary>
    private static bool SameNode(NodeBinding candidate, NodeBinding binding) =>
        binding.Stable
            ? candidate.Id == binding.Id
            : candidate.Role == binding.Role
                && candidate.Name == binding.Name
                && candidate.TestId == binding.TestId
                && SameRect(candidate.Rect, binding.Rect)
                && candidate.Likeness == binding.Likeness;

    /// <summary>Whether a node takes a press of its own, as the driver's hit test judges an element under the pointer.</summary>
    private static bool Pressable(NodeBinding binding) =>
        binding.Actions.Contains("Click") && !SelectedByClick.Contains(binding.Role ?? "");

    /// <summary>The ids of the nodes above the node <paramref name="id"/> in a tree, from the root down; none when the tree does not hold it.</summary>
    private static List<string> Ancestors(SemanticNode root, string id)
    {
        static List<string>? Above(SemanticNode node, string id)
        {
            if (node.Ref == id)
            {
                return [];
            }

            foreach (var child in node.Children)
            {
                if (Above(child, id) is { } below)
                {
                    return [node.Ref, .. below];
                }
            }

            return null;
        }

        return Above(root, id) ?? [];
    }

    /// <summary>Whether a rect holds a point.</summary>
    private static bool Holds(Rect rect, Point point) =>
        point.X >= rect.X && point.Y >= rect.Y && point.X < rect.X + rect.Width && point.Y < rect.Y + rect.Height;

    /// <summary>
    /// Runs the input that follows one that already changed the app (a clear before a type): its failure, whatever the driver says it sent, is
    /// <c>ACTION_MAY_HAVE_COMMITTED</c>, so the step is never repeated as if nothing happened.
    /// </summary>
    private static async Task AfterChangeAsync(string label, Func<Task> run)
    {
        try
        {
            await run().ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not OperationCanceledException)
        {
            throw new EngineException(
                "ACTION_MAY_HAVE_COMMITTED",
                $"{label}: {cause.Message} (an earlier command of this step changed the app)",
                retryable: false,
                cause
            );
        }
    }
}
