// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// Upstream's <c>agent/point-tap.ts</c>. Point taps: how a viewport point an executor names is routed onto the tree, and how the result
/// reads back. A point goes to the engine as a bare point, where the model aimed, and the trace keeps it inside the node under it; an
/// engine that taps listed nodes only gets the enabled control containing the point, tapped by its id. Coordinates read off a screenshot
/// are scaled into the observation's CSS pixels first.
/// </summary>
internal static class PointTap
{
    /// <summary>How a point result names a node: by the line the model holds for it (upstream's <c>PointProse</c>).</summary>
    public delegate string NodeProse(SemanticNode node);

    /// <summary>Upstream's <c>POINT_VERBS</c>: the node verb each point verb resolves onto on an engine without the bare-point kind, and how each reads back to the model.</summary>
    public static IReadOnlyDictionary<string, (string Node, string Did)> PointVerbs { get; } = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["tapAt"] = ("tap", "Tapped"),
        ["hoverAt"] = ("hover", "Hovered over"),
    };

    /// <summary>
    /// Upstream's <c>imagePointToViewport</c>: scales a point read off a screenshot into the observation's CSS pixels and clamps it to the
    /// viewport. <c>pixels.Scale</c> is image pixels per CSS pixel (3 on a device screenshot, 1 on a CSS-scale capture); the observation's
    /// own viewport scale is not consulted, because a device engine reports its geometry in logical points regardless of the image.
    /// </summary>
    public static Point ImagePointToViewport(Point point, ExecutorPixels pixels, TraceViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var scale = pixels.Scale > 0 ? pixels.Scale : 1;
        return Geometry.ClampToViewport(
            new Point(
                Math.Clamp(point.X, 0, Math.Max(0, pixels.Width - 1)) / scale,
                Math.Clamp(point.Y, 0, Math.Max(0, pixels.Height - 1)) / scale),
            viewport);
    }

    /// <summary>Upstream's <c>HitTest</c>: what the newest observation lists at a viewport point.</summary>
    /// <param name="Control">The innermost enabled control whose box contains the point, if any.</param>
    /// <param name="Under">The innermost listed node of any role whose box contains the point, if any.</param>
    public readonly record struct Hit(SemanticNode? Control, SemanticNode? Under);

    /// <summary>
    /// Upstream's <c>hitTest</c>: finds what the newest observation lists at a viewport point. Only an enabled interactive role counts as the
    /// control: tapping a node taps its center, which is what the caller asked for only when the node is the control itself, not a paragraph
    /// or region that happens to contain the point.
    /// </summary>
    public static Hit HitTest(Observation observation, Point point)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.TreeUnavailable)
        {
            return new Hit(null, null);
        }

        return new Hit(
            InnermostAt(observation, point, node => AgentObservation.InteractiveRoles.Contains(node.Role ?? "") && !node.States.Disabled),
            InnermostAt(observation, point, _ => true));
    }

    // The innermost listed node whose box contains the point, among those accept admits: deepest in the tree, then the smallest box. Hidden
    // nodes and nodes without a box are skipped. Every box is in the top-level viewport's CSS pixels, nested documents included, so a node
    // inside an iframe is found like any other.
    private static SemanticNode? InnermostAt(Observation observation, Point point, Func<SemanticNode, bool> accept)
    {
        SemanticNode? best = null;
        var bestDepth = -1;
        var bestArea = 0.0;

        void Visit(SemanticNode node, int depth)
        {
            if (node.Rect is { Width: > 0, Height: > 0 } rect && !node.States.Hidden && Geometry.ContainsPoint(rect, point) && accept(node))
            {
                var area = rect.Width * rect.Height;
                if (best is null || depth > bestDepth || (depth == bestDepth && area < bestArea))
                {
                    (best, bestDepth, bestArea) = (node, depth, area);
                }
            }

            foreach (var child in node.Children)
            {
                Visit(child, depth + 1);
            }
        }

        foreach (var root in observation.Roots)
        {
            Visit(root, 0);
        }

        return best;
    }

    /// <summary>
    /// Upstream's <c>nodeLine</c>: the observation's own line for a node, as the model already reads it; the id alone when the line was cut.
    /// </summary>
    public static string NodeLine(string text, string id)
    {
        foreach (var line in (text ?? "").Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#" + id + " ", StringComparison.Ordinal) || trimmed == "#" + id)
            {
                return trimmed;
            }
        }

        return "#" + id;
    }

    private static string At(Point point) => "(" + Num(point.X) + ", " + Num(point.Y) + ")";

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// Upstream's <c>describePointAction</c>: what one point action did, for the model that named the point: the control it acted on by id, the
    /// control it acted inside as a bare point, or the bare point on nothing listed.
    /// </summary>
    /// <param name="verb"><c>tapAt</c> or <c>hoverAt</c>.</param>
    /// <param name="point">The point, after it was clamped to the viewport.</param>
    /// <param name="control">The control at the point.</param>
    /// <param name="under">The innermost listed node at the point.</param>
    /// <param name="byId">Whether the control was acted on by id rather than at the bare point.</param>
    /// <param name="prose">How a node reads.</param>
    public static string DescribePointAction(string verb, Point point, SemanticNode? control, SemanticNode? under, bool byId, NodeProse prose)
    {
        ArgumentNullException.ThrowIfNull(prose);
        var did = PointVerbs[verb].Did;
        var at = At(point);
        if (control is not null)
        {
            return byId ? did + " " + prose(control) + ", the control at " + at + "." : did + " the point " + at + " inside " + prose(control) + ".";
        }

        return did + " the point " + at + "; no listed control is there" + (under is null ? "" : " (under it: " + prose(under) + ")") + ".";
    }

    /// <summary>
    /// Upstream's <c>describePointDrag</c>: what one point drag did, for the model that named the points: the two controls it joined by id, or
    /// the bare points with what each sits on.
    /// </summary>
    public static string DescribePointDrag(Point from, Point to, SemanticNode? source, SemanticNode? destination, SemanticNode? fromUnder, SemanticNode? toUnder, bool byId, NodeProse prose)
    {
        ArgumentNullException.ThrowIfNull(prose);
        if (byId && source is not null && destination is not null)
        {
            return "Dragged " + prose(source) + ", the control at " + At(from) + ", onto " + prose(destination) + ", the control at " + At(to) + ".";
        }

        string End(Point point, SemanticNode? control, SemanticNode? under) =>
            control is not null
                ? "the point " + At(point) + " inside " + prose(control)
                : "the point " + At(point) + (under is null ? "" : " (under it: " + prose(under) + ")");

        var neither = source is null && destination is null ? "; no listed control is at either end" : "";
        return "Dragged from " + End(from, source, fromUnder) + " to " + End(to, destination, toUnder) + neither + ".";
    }

    /// <summary>Upstream's <c>describePointHit</c>: what one hit test found, for the model that named the point.</summary>
    public static string DescribePointHit(Point point, SemanticNode? control, SemanticNode? under, NodeProse prose)
    {
        ArgumentNullException.ThrowIfNull(prose);
        var at = At(point);
        if (control is not null)
        {
            return prose(control) + ", the control at " + at;
        }

        return under is not null ? "no control is listed at " + at + "; under it: " + prose(under) : "nothing the screen lists is at " + at;
    }
}
