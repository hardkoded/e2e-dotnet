// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// A point of the viewport, in the space <see cref="SemanticNode.Rect"/> is in and <see cref="IEngineSession.PerformAtAsync(Point, PointerAction, CancellationToken)"/> dispatches in.
/// Upstream's <c>ViewportPoint</c>.
/// </summary>
public readonly record struct Point(double X, double Y);

/// <summary>
/// One action <see cref="IEngineSession.PerformAtAsync(Point, PointerAction, CancellationToken)"/> carries out at a viewport point, with no node behind it: the pointer half of
/// the action vocabulary, for a surface that has pixels and coordinates where the tree lists nothing (a canvas, a map, a desktop shell, a
/// game), and for a test that names a point outright (<c>Screen.TapAtAsync</c>, <c>Screen.SwipeAsync</c> from one point to another, a
/// tap's <c>Position</c>). The kinds are the pointer subset of <see cref="LocatorAction"/> with the same meanings; <see cref="DragTo"/>
/// ends at a second point, and <see cref="Swipe"/> is a scroll gesture started at the point. <see cref="SwipeTo"/> is the one kind with
/// no node form: the finger's path from the point to its target, a touch swipe on a device and a pointer drag on a document platform,
/// which is distinct from <see cref="DragTo"/> where a platform holds before it drags. Upstream's <c>PointerAction</c>.
/// </summary>
public abstract record PointerAction
{
    private PointerAction()
    {
    }

    public sealed record Tap : PointerAction;

    public sealed record DoubleTap : PointerAction;

    /// <summary>A hold. Without a duration the engine holds its own default.</summary>
    public sealed record LongPress(TimeSpan? Duration = null) : PointerAction;

    /// <summary>A drag to another point. With a duration the finger takes that long to get there.</summary>
    public sealed record SwipeTo(Point Target, TimeSpan? Duration = null) : PointerAction;

    public sealed record SecondaryTap : PointerAction;

    public sealed record Hover : PointerAction;

    public sealed record DragTo(Point Target) : PointerAction;

    /// <summary>A scroll gesture started at the point, as far as <paramref name="Momentum"/> says.</summary>
    public sealed record Swipe(ScrollDirection Direction, Momentum? Momentum = null) : PointerAction;
}

/// <summary>
/// The action kinds of the engine contract, by upstream's names, in contract order: what an engine declares in
/// <see cref="IEngineSession.ActionKinds"/> and <see cref="IEngineSession.PointerActionKinds"/>. Upstream's
/// <c>LOCATOR_ACTION_KINDS</c> and <c>POINTER_ACTION_KINDS</c>.
/// </summary>
public static class EngineActionKinds
{
    /// <summary>Every kind of <see cref="LocatorAction"/>.</summary>
    public static IReadOnlyList<string> Locator { get; } =
        ["tap", "doubleTap", "secondaryTap", "longPress", "fill", "clear", "press", "check", "uncheck", "focus", "hover", "scrollIntoView", "selectOption", "setInputFiles", "dragTo", "swipe"];

    /// <summary>Every kind of <see cref="PointerAction"/>.</summary>
    public static IReadOnlyList<string> Pointer { get; } =
        ["tap", "doubleTap", "secondaryTap", "longPress", "hover", "dragTo", "swipe", "swipeTo"];

    /// <summary>
    /// The upstream name of a locator action's kind. <see cref="LocatorAction.PressSequentially"/> is upstream's <c>focus</c> followed by
    /// the keyboard, so it needs the engine to declare <c>focus</c>.
    /// </summary>
    public static string Of(LocatorAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            LocatorAction.Tap => "tap",
            LocatorAction.DoubleTap => "doubleTap",
            LocatorAction.SecondaryTap => "secondaryTap",
            LocatorAction.LongPress => "longPress",
            LocatorAction.Fill => "fill",
            LocatorAction.Clear => "clear",
            LocatorAction.Press => "press",
            LocatorAction.PressSequentially => "focus",
            LocatorAction.Check => "check",
            LocatorAction.Uncheck => "uncheck",
            LocatorAction.Focus => "focus",
            LocatorAction.Hover => "hover",
            LocatorAction.ScrollIntoView => "scrollIntoView",
            LocatorAction.Select => "selectOption",
            LocatorAction.SetInputFiles => "setInputFiles",
            LocatorAction.DragTo => "dragTo",
            LocatorAction.Swipe => "swipe",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "unknown locator action"),
        };
    }

    /// <summary>The upstream name of a pointer action's kind.</summary>
    public static string Of(PointerAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            PointerAction.Tap => "tap",
            PointerAction.DoubleTap => "doubleTap",
            PointerAction.SecondaryTap => "secondaryTap",
            PointerAction.LongPress => "longPress",
            PointerAction.Hover => "hover",
            PointerAction.DragTo => "dragTo",
            PointerAction.Swipe => "swipe",
            PointerAction.SwipeTo => "swipeTo",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "unknown pointer action"),
        };
    }
}
