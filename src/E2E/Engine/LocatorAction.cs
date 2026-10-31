// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>A deterministic action <see cref="IEngineSession.PerformAsync"/> carries out on one node.</summary>
public abstract record LocatorAction
{
    private LocatorAction()
    {
    }

    /// <summary>A tap. <paramref name="Modifiers"/> are held for the click, and only an engine that declares <see cref="EngineCapabilities.TapModifiers"/> receives them.</summary>
    public sealed record Tap(IReadOnlyList<KeyModifier>? Modifiers = null) : LocatorAction;

    /// <summary>A double tap. <paramref name="Modifiers"/> are held for both clicks, as for <see cref="Tap"/>.</summary>
    public sealed record DoubleTap(IReadOnlyList<KeyModifier>? Modifiers = null) : LocatorAction;

    /// <summary>A right click, or the platform's equivalent. <paramref name="Modifiers"/> are held for the click, as for <see cref="Tap"/>.</summary>
    public sealed record SecondaryTap(IReadOnlyList<KeyModifier>? Modifiers = null) : LocatorAction;

    /// <summary>A press held on the node for <paramref name="Duration"/>, or for the engine's own default hold when it is null.</summary>
    public sealed record LongPress(TimeSpan? Duration = null) : LocatorAction;

    /// <summary>Moves the pointer over the node.</summary>
    public sealed record Hover : LocatorAction;

    /// <summary>Drags the node onto <paramref name="Target"/>, a node of the same observation.</summary>
    public sealed record DragTo(SemanticNode Target) : LocatorAction;

    public sealed record Fill(string Value, bool Sensitive = false) : LocatorAction;

    public sealed record Press(string Key) : LocatorAction;

    /// <summary>Focuses the node and types <paramref name="Text"/> one character at a time.</summary>
    public sealed record PressSequentially(string Text, TimeSpan? Delay = null) : LocatorAction;

    public sealed record Select(string Value) : LocatorAction;

    /// <summary>
    /// Sets the files of a file input, or of a control that opens a file chooser when clicked, such as a button over a hidden input.
    /// <paramref name="Paths"/> are absolute: the locator resolves them from the project root.
    /// </summary>
    public sealed record SetInputFiles(IReadOnlyList<string> Paths) : LocatorAction;

    public sealed record Check : LocatorAction;

    public sealed record Uncheck : LocatorAction;

    public sealed record Clear : LocatorAction;

    public sealed record Focus : LocatorAction;

    /// <summary>Scrolls the node's nearest scrollable container until the node is in the viewport.</summary>
    public sealed record ScrollIntoView : LocatorAction;

    /// <summary>Scrolls a scrollable node about three quarters of its height or width. The viewport swipe is <see cref="IEngineSession.SwipeAsync(ScrollDirection, CancellationToken)"/>.</summary>
    public sealed record Swipe(ScrollDirection Direction, Momentum? Momentum = null) : LocatorAction;
}

/// <summary>Which way a scroll moves the content into view. <see cref="Down"/> reveals what is below.</summary>
public enum ScrollDirection
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>Fling strength of a swipe: how far the gesture travels. Upstream's <c>Momentum</c> (<c>none</c>, <c>slow</c>, <c>fast</c>).</summary>
public enum Momentum
{
    /// <summary>No fling: half an extent on the web, half a node on a device.</summary>
    None,

    /// <summary>A short stride. The engine decides how far: a node swipe on a device is a quarter of the node, on the web three quarters of its extent.</summary>
    Slow,

    /// <summary>A long stride: four fifths of a node on a device, one and a half extents on the web.</summary>
    Fast,
}
