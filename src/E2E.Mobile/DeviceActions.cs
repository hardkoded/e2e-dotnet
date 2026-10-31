// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile;

/// <summary>One pointer action at a screen point in logical pixels, the space every node's bounds are in, with no element resolved behind it.</summary>
public abstract record PointerAction
{
    private PointerAction()
    {
    }

    public sealed record Tap : PointerAction;

    public sealed record DoubleTap : PointerAction;

    /// <summary>A hold. Without a duration it holds <see cref="DeviceActions.DefaultLongPress"/>.</summary>
    public sealed record LongPress(TimeSpan? Duration = null) : PointerAction;

    /// <summary>A drag to another point. With a duration the finger takes that long to get there.</summary>
    public sealed record SwipeTo(Point Target, TimeSpan? Duration = null) : PointerAction;

    public sealed record SecondaryTap : PointerAction;

    public sealed record Hover : PointerAction;

    public sealed record DragTo(Point Target) : PointerAction;

    public sealed record Swipe(ScrollDirection Direction) : PointerAction;
}

/// <summary>
/// What a <see cref="DeviceSession"/> does for each action of the contract: the translation of a node and an action
/// into agent-device commands, the transition wait a test's step makes, and the pointer dispatch that agrees with the
/// kinds a device declares.
/// </summary>
public sealed class DeviceActions : IDeviceActions
{
    /// <summary>
    /// How long a long press holds when the caller names no duration: the agent's <c>long_press</c> tool and a test's
    /// bare <c>longPress()</c>. agent-device's own default is under half a second, shorter than a React Native
    /// <c>Pressable</c>'s <c>delayLongPress</c> of 500 ms, let alone the longer thresholds apps set, so a default hold
    /// there registers as a tap. One second clears them the way Detox's default does. A test that needs more passes a
    /// duration.
    /// </summary>
    public static readonly TimeSpan DefaultLongPress = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The action kinds a device honors, declared so the harness offers exactly these: no <c>select</c> verb on a device
    /// (<c>selectOption</c>, <c>setInputFiles</c>, and <c>scrollIntoView</c> have no touch equivalent), and an action
    /// still refuses a declared kind one node cannot take (<c>check</c> on a toggle whose state is unknown,
    /// <c>focus</c> on anything but a field).
    /// </summary>
    public static IReadOnlyList<string> NodeActionKinds { get; } =
        ["tap", "doubleTap", "longPress", "fill", "clear", "press", "check", "uncheck", "focus", "hover", "dragTo", "swipe"];

    /// <summary>
    /// The pointer actions a device honors at a bare point: agent-device presses and swipes by coordinates, so taps,
    /// double taps, long presses, and a swipe to another point land anywhere. A coordinate hover, secondary tap, or drag
    /// (a hold before the move, unlike a swipe) has no touch equivalent it exposes without a node, and a directional
    /// swipe at a bare point has no length, so only the screen root scrolls that way.
    /// </summary>
    public static IReadOnlyList<string> PointerActionKinds { get; } = ["tap", "doubleTap", "longPress", "swipeTo"];

    public async Task PerformAsync(DeviceSession session, SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(action);

        // The harness's token does not abort at the action timeout, so each input enforces its own budget.
        var input = session.InputBudget(KindOf(action));

        // The observation root is the screen: a swipe on it scrolls the whole viewport, which is the one action a screen
        // takes. It sends no amount and keeps agent-device's default for every momentum: agent-device centres the
        // gesture on the viewport, so the finger starts at 0.5 + amount / 2 of the height, and a larger amount lands it
        // in bottom chrome (a sticky footer, a tab bar) that swallows the drag.
        if (node.Ref == SnapshotProjection.RootId)
        {
            if (action is not LocatorAction.Swipe screenSwipe)
            {
                throw DeviceSupport.NotActionable($"the screen root takes swipe only, not {KindOf(action)}; act on a node");
            }

            await ScrollScreenAsync(session, screenSwipe.Direction, input, cancellationToken).ConfigureAwait(false);
            return;
        }

        var entry = session.Resolve(node);
        var label = $"perform {KindOf(action)}";

        // A toggle already in the wanted state sends nothing, so it neither waits for a transition nor counts as an
        // action the next control must wait on.
        if (action is LocatorAction.Check or LocatorAction.Uncheck
            && entry.States.TryGetValue("checked", out var current)
            && current == (action is LocatorAction.Check))
        {
            return;
        }

        var before = session.LatestIndex;
        await session.ActAsync(
            label,
            async (client, token) =>
            {
                var settle = session.ActionSettle();
                var target = session.Origin == ActionOrigin.Test ? await session.SettledAsync(entry, token).ConfigureAwait(false) : entry;
                await RunAsync(client, target, action, settle, input, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        session.MarkAction(before);
    }

    private static async Task RunAsync(IDeviceClient client, NodeBinding target, LocatorAction action, JsonObject settle, InputBudget input, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case LocatorAction.Tap:
                await DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.InteractionsPress, Args(settle, DeviceSession.ActionTarget(target, control: true)), cancellationToken), cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Focus:
                // A touch surface focuses by tapping, and a tap on anything but an editable field activates it. Focus is
                // offered for fields only.
                if (target.Node.Role != "textbox")
                {
                    throw DeviceSupport.Unsupported($"agent-device can only focus editable fields; node {target.Id} is {target.Node.Role ?? "unknown"}");
                }

                await DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.InteractionsPress, Args(settle, DeviceSession.ActionTarget(target)), cancellationToken), cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.DoubleTap:
                await DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.InteractionsPress, Args(settle, DeviceSession.ActionTarget(target), ("count", JsonValue.Create(2))), cancellationToken), cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Fill fill:
                await DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.InteractionsFill, Args(settle, DeviceSession.ActionTarget(target), ("text", JsonValue.Create(fill.Value))), cancellationToken), cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Clear:
                await DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.InteractionsFill, Args(settle, DeviceSession.ActionTarget(target), ("text", JsonValue.Create(""))), cancellationToken), cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Check or LocatorAction.Uncheck:
                // A toggle whose state the tree does not expose (Android switches) cannot be set, only flipped.
                // Flipping blind could undo a correct state.
                if (!target.States.TryGetValue("checked", out var isChecked))
                {
                    throw DeviceSupport.Unsupported($"agent-device cannot read whether node {target.Id} is checked; tap it instead");
                }

                if (isChecked != (action is LocatorAction.Check))
                {
                    await DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.InteractionsPress, Args(settle, DeviceSession.ActionTarget(target, control: true)), cancellationToken), cancellationToken).ConfigureAwait(false);
                }

                break;
            case LocatorAction.Press press:
                await PressKeyAsync(client, target, press.Key, settle, input, cancellationToken).ConfigureAwait(false);
                break;
            case LocatorAction.Swipe swipe:
                await SwipeNodeAsync(client, target, swipe.Direction, swipe.Momentum, input, cancellationToken).ConfigureAwait(false);
                break;
            default:
                // Not in NodeActionKinds, so the harness never sends them. A device has no touch equivalent.
                throw DeviceSupport.Unsupported($"agent-device cannot perform \"{KindOf(action)}\" on a device surface");
        }
    }

    /// <summary>
    /// Keys on a touch surface, in the contract's key grammar: <c>Enter</c> submits through the soft keyboard,
    /// <c>Space</c> and any single character are typed into the focused field. A field that is not focused is tapped first. agent-device exposes no key event bus,
    /// so modifiers and the other named keys (<c>Escape</c>, <c>Tab</c>, <c>Backspace</c>, the arrows) have nothing to
    /// land on and are refused.
    /// </summary>
    private static async Task PressKeyAsync(IDeviceClient client, NodeBinding entry, string key, JsonObject settle, InputBudget input, CancellationToken cancellationToken)
    {
        var typed = SoftKeyboardKey(key);

        // A key reaches the field through the soft keyboard, which is up only while the field holds focus. Each command of the
        // input is sent on its own, so no keystroke follows a focus tap that used the budget up.
        if (entry.Node.Role == "textbox" && !(entry.States.TryGetValue("focused", out var focused) && focused))
        {
            await DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.InteractionsPress, Args(settle, DeviceSession.ActionTarget(entry)), cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        if (typed is null)
        {
            await DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.CommandKeyboard, DeviceArgs.Of(("action", JsonValue.Create("enter"))), cancellationToken), cancellationToken).ConfigureAwait(false);
            return;
        }

        await DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.InteractionsType, DeviceArgs.Of(("text", JsonValue.Create(typed))), cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// What a key reaches on the soft keyboard: null for <c>Enter</c>, the text to type for <c>Space</c> and a single
    /// character. Any other key, and every modifier, is refused.
    /// </summary>
    private static string? SoftKeyboardKey(string key)
    {
        var parsed = DeviceKeys.Parse(key)
            ?? throw DeviceSupport.Unsupported($"\"{key}\" is not a key: press takes one key in the form [Modifier+]...Key, a named key ({string.Join(", ", DeviceKeys.Names)}) or one character");
        if (parsed.Modifiers.Count > 0)
        {
            throw DeviceSupport.Unsupported($"agent-device cannot hold {string.Join('+', parsed.Modifiers)} on a device surface; press the key alone");
        }

        return parsed switch
        {
            { Name: "Enter" } => null,
            { Name: "Space" } => " ",
            { Name: null } => parsed.Character,
            _ => throw DeviceSupport.Unsupported($"agent-device cannot press {parsed.Name} on a device surface; only Enter, Space, and single characters reach the soft keyboard"),
        };
    }

    private static async Task SwipeNodeAsync(IDeviceClient client, NodeBinding target, ScrollDirection direction, Momentum? momentum, InputBudget? input, CancellationToken cancellationToken)
    {
        if (target.Node.Rect is not { } rect)
        {
            throw DeviceSupport.NotActionable($"node {target.Id} has no bounds to swipe within");
        }

        var (from, to) = DeviceSupport.SwipeWithin(new Rect(rect.X, rect.Y, rect.Width, rect.Height), direction, momentum);
        await DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.InteractionsSwipe, DeviceArgs.Of(("from", DeviceSession.PointNode(from)), ("to", DeviceSession.PointNode(to))), cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private static async Task ScrollScreenAsync(DeviceSession session, ScrollDirection direction, InputBudget? input, CancellationToken cancellationToken)
    {
        var before = session.LatestIndex;
        await session.CommandAsync(
            "swipe",
            (client, token) => DeviceSession.SendInputAsync(
                input,
                () => client.CallAsync(DeviceMethods.InteractionsScroll, Args(session.ActionSettle(), ("direction", JsonValue.Create(DirectionName(direction)))), token),
                token),
            cancellationToken).ConfigureAwait(false);
        session.MarkAction(before);
    }

    public Task PressAsync(DeviceSession session, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return PressFocusedAsync(session, key, session.InputBudget("press"), cancellationToken);
    }

    /// <summary>
    /// One key to the focused field, with the same reach as a node press: Enter submits through the soft keyboard, Space
    /// and single characters are typed. Modifiers and the other named keys have no event bus to land on.
    /// </summary>
    private static async Task PressFocusedAsync(DeviceSession session, string key, InputBudget input, CancellationToken cancellationToken)
    {
        var typed = SoftKeyboardKey(key);
        var before = session.LatestIndex;
        if (typed is null)
        {
            await session.CommandAsync(
                "keyboard.press",
                (client, token) => DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.CommandKeyboard, DeviceArgs.Of(("action", JsonValue.Create("enter"))), token), token),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await session.CommandAsync(
                "keyboard.press",
                (client, token) => DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.InteractionsType, DeviceArgs.Of(("text", JsonValue.Create(typed))), token), token),
                cancellationToken).ConfigureAwait(false);
        }

        session.MarkAction(before);
    }

    public Task SwipeAsync(DeviceSession session, ScrollDirection direction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ScrollScreenAsync(session, direction, session.InputBudget("swipe"), cancellationToken);
    }

    public Task<EngineScreenshot> ScreenshotAsync(DeviceSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.MaskedScreenshotAsync(cancellationToken);
    }

    // --- what the contract has no verb for ---

    /// <summary>A hold on a node. Without a duration it holds <see cref="DefaultLongPress"/>.</summary>
    internal static Task LongPressAsync(DeviceSession session, SemanticNode node, TimeSpan? duration, CancellationToken cancellationToken) =>
        RunNodeAsync(session, node, "longPress", (client, target, settle, input, token) =>
            DeviceSession.SendInputAsync(
                input,
                () => client.CallAsync(
                    DeviceMethods.InteractionsLongPress,
                    Args(settle, DeviceSession.ActionTarget(target), ("durationMs", JsonValue.Create((duration ?? DefaultLongPress).TotalMilliseconds))),
                    token),
                token,
                hold: duration ?? DefaultLongPress),
            cancellationToken);

    /// <summary>A pointer hover over a node. agent-device sends no settle wait with it.</summary>
    internal static Task HoverAsync(DeviceSession session, SemanticNode node, CancellationToken cancellationToken) =>
        RunNodeAsync(session, node, "hover", (client, target, _, input, token) =>
            DeviceSession.SendInputAsync(input, () => client.CallAsync(DeviceMethods.InteractionsHover, DeviceArgs.Of(DeviceSession.ActionTarget(target)), token), token), cancellationToken);

    /// <summary>A drag from one node to another.</summary>
    internal static Task DragToAsync(DeviceSession session, SemanticNode node, SemanticNode destination, CancellationToken cancellationToken) =>
        RunNodeAsync(session, node, "dragTo", (client, target, _, input, token) =>
        {
            // A test locates the drop target in a capture of its own, after the source's; agent-device takes both refs
            // from the newest one only.
            var source = session.Current(target);
            var to = session.Current(session.Resolve(destination));
            return DeviceSession.SendInputAsync(
                input,
                () => client.CallAsync(
                    DeviceMethods.InteractionsDrag,
                    DeviceArgs.Of(("source", DeviceSession.ActionTarget(source).Value), ("destination", DeviceSession.ActionTarget(to).Value)),
                    token),
                token);
        }, cancellationToken);

    /// <summary>A swipe inside a node's bounds, as far as <paramref name="momentum"/> says.</summary>
    internal static Task SwipeAsync(DeviceSession session, SemanticNode node, ScrollDirection direction, Momentum? momentum, CancellationToken cancellationToken) =>
        node.Ref == SnapshotProjection.RootId
            ? ScrollScreenAsync(session, direction, session.InputBudget("swipe"), cancellationToken)
            : RunNodeAsync(session, node, "swipe", (client, target, _, input, token) => SwipeNodeAsync(client, target, direction, momentum, input, token), cancellationToken);

    private static async Task RunNodeAsync(
        DeviceSession session,
        SemanticNode node,
        string kind,
        Func<IDeviceClient, NodeBinding, JsonObject, InputBudget, CancellationToken, Task> run,
        CancellationToken cancellationToken)
    {
        var input = session.InputBudget(kind);
        var label = $"perform {kind}";
        var entry = session.Resolve(node);
        var before = session.LatestIndex;
        await session.ActAsync(
            label,
            async (client, token) =>
            {
                var settle = session.ActionSettle();
                var target = session.Origin == ActionOrigin.Test ? await session.SettledAsync(entry, token).ConfigureAwait(false) : entry;
                await run(client, target, settle, input, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        session.MarkAction(before);
    }

    /// <summary>
    /// Issues one pointer action at a screen point in logical pixels. A double tap is two presses (<c>count: 2</c>), as
    /// it is on a node: agent-device's own double-tap gesture reaches a React Native <c>Pressable</c> on iOS as one
    /// press, while two presses in a row land about 285 ms apart. The kinds outside <see cref="PointerActionKinds"/> are
    /// refused here.
    /// </summary>
    internal static Task<JsonNode?> PointerInteractionAsync(IDeviceClient client, Point point, PointerAction action, JsonObject settle, CancellationToken cancellationToken)
    {
        (string, JsonNode?)[] at = [("x", JsonValue.Create(point.X)), ("y", JsonValue.Create(point.Y))];
        switch (action)
        {
            case PointerAction.Tap:
                return client.CallAsync(DeviceMethods.InteractionsPress, Args(settle, at), cancellationToken);
            case PointerAction.DoubleTap:
                return client.CallAsync(DeviceMethods.InteractionsPress, Args(settle, [.. at, ("count", JsonValue.Create(2))]), cancellationToken);
            case PointerAction.LongPress hold:
                return client.CallAsync(
                    DeviceMethods.InteractionsLongPress,
                    Args(settle, [.. at, ("durationMs", JsonValue.Create((hold.Duration ?? DefaultLongPress).TotalMilliseconds))]),
                    cancellationToken);
            case PointerAction.SwipeTo { Duration: { } duration } swipe:
                return client.CallAsync(
                    DeviceMethods.InteractionsPan,
                    DeviceArgs.Of(
                        ("x", JsonValue.Create(point.X)),
                        ("y", JsonValue.Create(point.Y)),
                        ("dx", JsonValue.Create(swipe.Target.X - point.X)),
                        ("dy", JsonValue.Create(swipe.Target.Y - point.Y)),
                        ("durationMs", JsonValue.Create(duration.TotalMilliseconds))),
                    cancellationToken);
            case PointerAction.SwipeTo swipe:
                return client.CallAsync(DeviceMethods.InteractionsSwipe, DeviceArgs.Of(("from", DeviceSession.PointNode(point)), ("to", DeviceSession.PointNode(swipe.Target))), cancellationToken);
            default:
                return Task.FromException<JsonNode?>(DeviceSupport.Unsupported($"agent-device cannot perform \"{PointerKindOf(action)}\" at a bare point"));
        }
    }

    internal static string DirectionName(ScrollDirection direction) => direction switch
    {
        ScrollDirection.Up => "up",
        ScrollDirection.Down => "down",
        ScrollDirection.Left => "left",
        ScrollDirection.Right => "right",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    /// <summary>The kind of an action as the contract names it, for a message.</summary>
    private static string KindOf(LocatorAction action) => action switch
    {
        LocatorAction.Tap => "tap",
        LocatorAction.DoubleTap => "doubleTap",
        LocatorAction.Fill => "fill",
        LocatorAction.Press => "press",
        LocatorAction.PressSequentially => "pressSequentially",
        LocatorAction.Select => "selectOption",
        LocatorAction.SetInputFiles => "setInputFiles",
        LocatorAction.Check => "check",
        LocatorAction.Uncheck => "uncheck",
        LocatorAction.Clear => "clear",
        LocatorAction.Focus => "focus",
        LocatorAction.ScrollIntoView => "scrollIntoView",
        LocatorAction.Swipe => "swipe",
        _ => action.GetType().Name,
    };

    private static string PointerKindOf(PointerAction action) => action switch
    {
        PointerAction.Tap => "tap",
        PointerAction.DoubleTap => "doubleTap",
        PointerAction.LongPress => "longPress",
        PointerAction.SwipeTo => "swipeTo",
        PointerAction.SecondaryTap => "secondaryTap",
        PointerAction.Hover => "hover",
        PointerAction.DragTo => "dragTo",
        PointerAction.Swipe => "swipe",
        _ => action.GetType().Name,
    };

    /// <summary>The options of one command: the settle wait, then the fields the action names.</summary>
    private static JsonObject Args(JsonObject settle, params (string Key, JsonNode? Value)[] fields)
    {
        var args = DeviceArgs.Of(fields);
        foreach (var (key, value) in settle)
        {
            args[key] = value?.DeepClone();
        }

        return args;
    }
}
