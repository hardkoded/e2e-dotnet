// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.WinApp;

/// <summary>A point on the screen, in physical pixels.</summary>
public readonly record struct Point(double X, double Y);

/// <summary>A box on the screen, in physical pixels.</summary>
public readonly record struct Rect(double X, double Y, double Width, double Height);

/// <summary>The logical size of the app window, in device-independent pixels.</summary>
public readonly record struct ViewportSize(double Width, double Height);

/// <summary>
/// One UI Automation element as the driver reads it: the fields of the winapp library's <c>UiElement</c> that the projection
/// uses, with no library type on it, so the projection and its tests run on any platform. Bounds are in physical screen pixels,
/// as UI Automation reports them. A flag that is null is unavailable, not false.
/// </summary>
public sealed record RawElement
{
    /// <summary>The driver's key for the element: what every action names it by.</summary>
    public required string Handle { get; init; }

    /// <summary>The UI Automation control type, such as <c>Button</c> or <c>Edit</c>.</summary>
    public string? Type { get; init; }

    /// <summary>The accessible name, usually the visible label.</summary>
    public string? Name { get; init; }

    public string? AutomationId { get; init; }

    /// <summary>The framework class, such as <c>ToggleSwitch</c> or <c>PasswordBox</c>, when the framework reports one.</summary>
    public string? ClassName { get; init; }

    public bool IsEnabled { get; init; } = true;

    public bool IsOffscreen { get; init; }

    /// <summary>The element's box on the screen; null when it has no size.</summary>
    public Rect? Bounds { get; init; }

    public string? Value { get; init; }

    /// <summary><c>On</c>, <c>Off</c>, or <c>Indeterminate</c> for a toggle, in any case (the library lower-cases them); null otherwise.</summary>
    public string? ToggleState { get; init; }

    /// <summary>Whether a selection item (a radio button, a tab, a list item) is the selected one; null for anything else.</summary>
    public bool? IsSelected { get; init; }

    /// <summary><c>Expanded</c>, <c>Collapsed</c>, <c>PartiallyExpanded</c>, or <c>LeafNode</c> in any case (the library lower-cases them); null without an expand pattern.</summary>
    public string? ExpandState { get; init; }

    /// <summary>The axes the element scrolls along, as the library spells them; null or empty when it does not scroll.</summary>
    public string? ScrollDir { get; init; }

    /// <summary>Whether the element takes a pattern action (invoke, toggle, select, expand) without a mouse.</summary>
    public bool IsInvokable { get; init; }

    /// <summary>Whether the element is a password field. Null when the driver did not ask.</summary>
    public bool? IsPassword { get; init; }

    /// <summary>True when the walk stopped below the element, so children are missing.</summary>
    public bool? HasMoreChildren { get; init; }

    public IReadOnlyList<RawElement> Children { get; init; } = [];
}

/// <summary>One top-level window of the app.</summary>
/// <param name="Handle">The window handle.</param>
/// <param name="ProcessId">The process that owns it.</param>
/// <param name="Title">Its title, empty for an untitled window.</param>
/// <param name="Bounds">Its box on the screen, in physical pixels.</param>
/// <param name="Scale">Physical pixels per device-independent pixel on the window's monitor.</param>
public sealed record WindowInfo(long Handle, int ProcessId, string Title, Rect Bounds, double Scale);

/// <summary>A window's pixels as the driver captured them: BGRA, rows top to bottom.</summary>
public sealed record RawScreenshot(byte[] Bgra, int Width, int Height);

/// <summary>The app a session launched or attached to.</summary>
/// <param name="ProcessId">The process the windows belong to.</param>
/// <param name="MainWindow">The window the launch found first; the session falls back to the largest when it is gone.</param>
/// <param name="Name">The app's name: the executable's file name, or the process name it was attached by.</param>
/// <param name="Owned">True when the session started the process and so ends it on a relaunch and at the end of the attempt.</param>
public sealed record LaunchedApp(int ProcessId, long MainWindow, string Name, bool Owned);
