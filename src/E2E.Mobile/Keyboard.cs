// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Mobile;

/// <summary>Where the keyboard-dismissing drag may start on a screen without pressing anything. Upstream's <c>keyboard.ts</c>.</summary>
internal static class Keyboard
{
    /// <summary>
    /// Roles no drag starts on, whether or not their children are listed: the ones a touch presses, toggles, or
    /// focuses, and the bars, where a drag never reaches the content.
    /// </summary>
    private static readonly HashSet<string> ControlRoles =
    [
        "button", "link", "textbox", "searchbox", "combobox", "checkbox", "radio", "switch", "tab", "menuitem", "option",
        "slider", "spinbutton", "listitem", "navigation", "toolbar", "tablist",
    ];

    /// <summary>Platform kinds that scroll, so a short drag on them reads as the touch that sends the keyboard away.</summary>
    private static readonly HashSet<string> ScrollKinds = ["scroll-view", "table", "collection-view", "recycler-view", "list-view"];

    /// <summary>Roles a touch does nothing to: a leaf with one of these is content, not a control.</summary>
    private static readonly HashSet<string> StaticRoles = ["text", "image", "heading", "status", "progressbar"];

    /// <summary>
    /// How far a drag starts from any control, in points: a control's touch area runs past its frame (React Native's
    /// <c>hitSlop</c>, UIKit's own padding), and the drag's first few points are inside touch slop, where it still
    /// counts as a tap on whatever it started on.
    /// </summary>
    private const double ControlClearance = 8;

    /// <summary>
    /// The share of the screen a leaf covers past which it is content merged into one element (a flattened tree), not
    /// a control: the tree shows nothing inside it to steer clear of.
    /// </summary>
    private const double MergedShare = 0.5;

    /// <summary>The share of each screen edge no drag starts in: the status bar, the home indicator, and the system edge gestures live there.</summary>
    private const double EdgeShare = 0.1;

    /// <summary>How many candidate points the search lays across the screen's shorter side.</summary>
    private const int GridSteps = 40;

    /// <summary>
    /// Where a keyboard-dismissing drag starts: the point nearest the middle of the screen that is above the keyboard
    /// and clear of every control. When the screen has a scroll view the point must be inside one, since a scroll view
    /// is what turns the drag into a dismissal and a drag anywhere else does nothing. A control is a node a touch acts
    /// on: one with a control role, or a leaf that is not plain content (text, an image), since a React Native
    /// <c>Pressable</c> with no role is an unnamed leaf like any other. A <c>Text</c> with its own <c>onPress</c> reads
    /// as plain text and is one this misses; a control inside a flattened tree, which shows as one leaf over most of
    /// the screen, is the other. Null when no point qualifies: a drag there would press a control or do nothing.
    /// </summary>
    public static Point? KeyboardDragStart(ProjectedSnapshot projected, ViewportSize viewport, double keyboardTop)
    {
        List<BoundingBox> controls = [];
        List<BoundingBox> scrollViews = [];
        var merged = viewport.Width * viewport.Height * MergedShare;
        foreach (var entry in projected.Index)
        {
            var node = entry.Node;
            if (node.Rect is not { } rect || node.States.Hidden || SnapshotProjection.ScreenKinds.Contains(entry.Kind))
            {
                continue;
            }

            if (ScrollKinds.Contains(entry.Kind))
            {
                scrollViews.Add(rect);
            }
            else if ((node.Role is not null && ControlRoles.Contains(node.Role))
                || (node.Children.Count == 0 && !StaticRoles.Contains(node.Role ?? "") && rect.Width * rect.Height <= merged))
            {
                controls.Add(Grow(rect, ControlClearance));
            }
        }

        var left = viewport.Width * EdgeShare;
        var right = viewport.Width * (1 - EdgeShare);
        var top = viewport.Height * EdgeShare;
        var bottom = Math.Min(viewport.Height * (1 - EdgeShare), keyboardTop - ControlClearance);
        var centre = new Point(viewport.Width / 2, viewport.Height / 2);

        // The grid is anchored on the centre, so a screen with nothing there starts where a user's thumb would.
        var step = Math.Min(viewport.Width, viewport.Height) / GridSteps;
        Point? best = null;
        var bestDistance = 0.0;
        for (var y = centre.Y - (Math.Floor((centre.Y - top) / step) * step); y <= bottom; y += step)
        {
            for (var x = centre.X - (Math.Floor((centre.X - left) / step) * step); x <= right; x += step)
            {
                var point = new Point(x, y);
                if (scrollViews.Count > 0 && !scrollViews.Any(view => Contains(view, point)))
                {
                    continue;
                }

                if (controls.Any(control => Contains(control, point)))
                {
                    continue;
                }

                var distance = Math.Sqrt(((x - centre.X) * (x - centre.X)) + ((y - centre.Y) * (y - centre.Y)));
                if (best is null || distance < bestDistance)
                {
                    best = point;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    /// <summary>A rect grown by <paramref name="margin"/> on every side.</summary>
    private static BoundingBox Grow(BoundingBox rect, double margin) =>
        new(rect.X - margin, rect.Y - margin, rect.Width + (margin * 2), rect.Height + (margin * 2));

    /// <summary>Whether a point lies inside a rect, edges included.</summary>
    private static bool Contains(BoundingBox rect, Point point) =>
        point.X >= rect.X && point.X <= rect.X + rect.Width && point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
}
