// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// Upstream's <c>agent/scroll-target.ts</c>: re-finding the node a scroll addresses. A scroll target is what sits where it sits more
/// than what it says: a device names a scroll view after its first visible row and renumbers the tree on every look, so the id the
/// model scrolled a moment ago may name nothing on the newest screen and its descriptor may re-find nothing either. The node of the
/// same role whose box is the old box, near enough, is the same list. A list that filled the screen and cannot be re-found at all
/// scrolls as the viewport does, which is what scrolling the main list does; a smaller region that vanished is gone, as for any
/// other action.
/// </summary>
internal static class ScrollTarget
{
    // How much of its old box a node must cover, and be covered by, to count as the same list in a new place in the tree: enough that
    // a carousel inside the list never stands in for the list, nor the list for a carousel.
    private const double SamePlaceOverlap = 0.5;

    /// <summary>
    /// Upstream's <c>resolveScrollTarget</c>: the node a scroll target names now, which is the node itself, its descriptor's match, or
    /// the same-role node in its old place. Null for a lost list that filled the screen, which the caller scrolls as the viewport.
    /// </summary>
    public static Resolved? ResolveScrollTarget(ObservationFeed feed, ExecutorTarget target)
    {
        ArgumentNullException.ThrowIfNull(feed);
        var (id, node, observation) = feed.Lookup(target);
        if (node is not null)
        {
            return new Resolved(node, observation);
        }

        var seen = feed.LastSeen(id);
        if (seen?.Node.Rect is not { } box)
        {
            throw ObservationFeed.NodeGone(id);
        }

        if (SamePlace(observation, seen.Node.Role, box) is { } again)
        {
            return new Resolved(again, observation);
        }

        if (seen.Observation.Viewport is { } viewport && Geometry.ViewportShare(box, viewport) >= CacheKeys.MainListShare)
        {
            return null;
        }

        throw ObservationFeed.NodeGone(id);
    }

    /// <summary>Upstream's <c>samePlace</c>: the listed node of the role whose box overlaps <paramref name="box"/> most, when it overlaps enough.</summary>
    public static SemanticNode? SamePlace(Observation observation, string? role, BoundingBox box)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(box);
        SemanticNode? best = null;
        var bestOverlap = 0.0;
        foreach (var node in LocatorResolver.Walk(observation.Roots))
        {
            if (!string.Equals(node.Role, role, StringComparison.Ordinal) || node.Rect is not { } rect || node.States.Hidden)
            {
                continue;
            }

            var overlap = Geometry.OverlapShare(rect, box);
            if (overlap >= SamePlaceOverlap && (best is null || overlap > bestOverlap))
            {
                best = node;
                bestOverlap = overlap;
            }
        }

        return best;
    }
}
