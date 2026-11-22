// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Internal;

/// <summary>Point and box arithmetic, as upstream's <c>internal/geometry.ts</c>.</summary>
internal static partial class Geometry
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
        return width <= 0 || height <= 0
            ? 0
            : Math.Clamp(width * height / ((double)viewport.Width * viewport.Height), 0, 1);
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
            Math.Clamp(Math.Floor(point.Y + 0.5), 0, Math.Max(0, viewport.Height - 1))
        );
    }

    /// <summary>Upstream's <c>isPercentage</c>: whether a coordinate is a percentage of the viewport from <c>'0%'</c> through <c>'100%'</c>.</summary>
    public static bool IsPercentage(ScreenCoordinate value) =>
        value.Percentage is { } text
        && PercentagePattern().IsMatch(text)
        && double.Parse(text.AsSpan(0, text.Length - 1), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)
            <= 100;

    /// <summary>Upstream's <c>isPixelPoint</c>: whether both coordinates of a point are CSS pixels, so it resolves without reading the viewport.</summary>
    public static bool IsPixelPoint(ScreenPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return point.X.Percentage is null && point.Y.Percentage is null;
    }

    /// <summary>
    /// Upstream's <c>resolveScreenPoint</c>: resolves a point a test names to CSS pixels. A percentage is that share of the viewport's
    /// width (x) or height (y), rounded to a whole pixel and kept on the last one, so <c>'100%'</c> stays inside the viewport. Pixels
    /// pass through.
    /// </summary>
    public static Point ResolveScreenPoint(ScreenPoint point, TraceViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(viewport);
        return new Point(ResolveCoordinate(point.X, viewport.Width), ResolveCoordinate(point.Y, viewport.Height));
    }

    /// <summary>Digits with an optional fraction, then <c>%</c>: no sign, exponent, or space.</summary>
    [GeneratedRegex(@"\A[0-9]+(?:\.[0-9]+)?%\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PercentagePattern();

    /// <summary>One coordinate of <see cref="ResolveScreenPoint"/> along a viewport side of <paramref name="size"/> CSS pixels. JavaScript's <c>Math.round</c> rounds halves up.</summary>
    private static double ResolveCoordinate(ScreenCoordinate value, int size) =>
        value.Percentage is not { } text
            ? value.Pixels
            : Math.Clamp(
                Math.Floor(
                    double.Parse(text.AsSpan(0, text.Length - 1), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)
                        / 100
                        * size
                        + 0.5
                ),
                0,
                Math.Max(0, size - 1)
            );

    /// <summary>
    /// Upstream's <c>VIEWPORT_LIMITS</c>: the sides a resized viewport may have, in CSS pixels. Narrower than any phone layout is no layout;
    /// wider than 4K is no screen.
    /// </summary>
    public static class ViewportLimits
    {
        public const int Min = 200;

        public const int Max = 4096;

        /// <summary>Whether <paramref name="side"/> is within the limits.</summary>
        public static bool Fits(int side) => side is >= Min and <= Max;
    }

    /// <summary>
    /// Upstream's <c>requireViewportSize</c>: checks a viewport size the agent asked for. Sides are whole CSS pixels within
    /// <see cref="ViewportLimits"/>, or this is <c>INVALID_ARGUMENT</c>.
    /// </summary>
    public static TraceViewport RequireViewportSize(TraceViewport? size) =>
        size is { } viewport && ViewportLimits.Fits(viewport.Width) && ViewportLimits.Fits(viewport.Height)
            ? viewport
            : throw new TestException(
                "INVALID_ARGUMENT",
                $"resize requires {{ width, height }} in whole CSS pixels from {ViewportLimits.Min} to {ViewportLimits.Max}"
            );

    /// <summary>Upstream's <c>containsPoint</c>: true when the point lies inside the box, on its top and left edges and short of its bottom and right ones.</summary>
    public static bool ContainsPoint(BoundingBox box, Point point)
    {
        ArgumentNullException.ThrowIfNull(box);
        return point.X >= box.X && point.X < box.X + box.Width && point.Y >= box.Y && point.Y < box.Y + box.Height;
    }
}
