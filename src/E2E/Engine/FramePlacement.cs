// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>Where a child document's node boxes land in the top-level viewport (upstream's <c>web/observation.ts</c>).</summary>
internal static class FramePlacement
{
    /// <summary>
    /// A node box a child document measured against its own viewport, in the top-level viewport's space: <paramref name="rect"/> shifted by
    /// the frame's box and clipped to it, empty when nothing of it is inside the frame. <paramref name="origin"/> is shifted but never
    /// clipped, and a clip that moves the box's corner keeps the unclipped corner as the origin, so an offset into the node lands where it
    /// would at the top level. Upstream's <c>placeBoxInFrame</c>.
    /// </summary>
    internal static (BoundingBox? Rect, Point? Origin) PlaceBoxInFrame(BoundingBox rect, Point? origin, BoundingBox frame)
    {
        var x0 = Math.Max(rect.X + frame.X, frame.X);
        var y0 = Math.Max(rect.Y + frame.Y, frame.Y);
        var x1 = Math.Min(rect.X + frame.X + rect.Width, frame.X + frame.Width);
        var y1 = Math.Min(rect.Y + frame.Y + rect.Height, frame.Y + frame.Height);
        if (x1 <= x0 || y1 <= y0)
        {
            return (null, null);
        }

        var placed = new BoundingBox(x0, y0, x1 - x0, y1 - y0);
        var corner = new Point((origin?.X ?? rect.X) + frame.X, (origin?.Y ?? rect.Y) + frame.Y);
        return corner.X == placed.X && corner.Y == placed.Y ? (placed, null) : (placed, corner);
    }
}
