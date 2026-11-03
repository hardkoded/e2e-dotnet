// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Internal;

/// <summary>Point and box arithmetic, as upstream's <c>internal/geometry.ts</c>.</summary>
internal static class Geometry
{
    /// <summary>
    /// Upstream's <c>overlapShare</c>: how much two boxes are the same box, the area they share over the area either covers. 0 for
    /// boxes apart and 1 for the same box.
    /// </summary>
    public static double OverlapShare(BoundingBox a, BoundingBox b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var width = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        var height = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var shared = width * height;
        var union = a.Width * a.Height + b.Width * b.Height - shared;
        return union <= 0 ? 0 : shared / union;
    }

    /// <summary>
    /// Upstream's <c>viewportShare</c>: the share of a viewport a box covers, 0 to 1. Only the part of the box inside the viewport
    /// counts, so a tall list mostly below the fold is not the main list. 0 for a viewport without area.
    /// </summary>
    public static double ViewportShare(BoundingBox box, TraceViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(viewport);
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return 0;
        }

        var width = Math.Min(box.X + box.Width, viewport.Width) - Math.Max(box.X, 0);
        var height = Math.Min(box.Y + box.Height, viewport.Height) - Math.Max(box.Y, 0);
        return width <= 0 || height <= 0 ? 0 : Math.Clamp(width * height / ((double)viewport.Width * viewport.Height), 0, 1);
    }

    /// <summary>
    /// Upstream's <c>requireFinitePoint</c>: a point of two finite numbers, or <c>INVALID_ARGUMENT</c> naming <paramref name="what"/>
    /// asked for it. Callers layer their own rule on top: the agent clamps into the viewport, a test refuses negatives.
    /// </summary>
    public static Point RequireFinitePoint(Point point, string what) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y)
            ? point
            : throw new TestException("INVALID_ARGUMENT", what + " requires a point { x, y } of finite numbers");

    /// <summary>
    /// Upstream's <c>clampToViewport</c>: rounds a point to whole CSS pixels and keeps it inside a box of the given size, so a point
    /// placed off the edge lands on the edge rather than failing the engine. JavaScript's <c>Math.round</c> rounds halves up.
    /// </summary>
    public static Point ClampToViewport(Point point, TraceViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        return new Point(
            Math.Clamp(Math.Floor(point.X + 0.5), 0, Math.Max(0, viewport.Width - 1)),
            Math.Clamp(Math.Floor(point.Y + 0.5), 0, Math.Max(0, viewport.Height - 1)));
    }

    /// <summary>Upstream's <c>containsPoint</c>: true when the point lies inside the box, on its top and left edges and short of its bottom and right ones.</summary>
    public static bool ContainsPoint(BoundingBox box, Point point)
    {
        ArgumentNullException.ThrowIfNull(box);
        return point.X >= box.X && point.X < box.X + box.Width && point.Y >= box.Y && point.Y < box.Y + box.Height;
    }
}
