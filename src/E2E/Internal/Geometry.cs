// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Internal;

/// <summary>Point and box arithmetic, as upstream's <c>internal/geometry.ts</c>.</summary>
internal static class Geometry
{
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
}
