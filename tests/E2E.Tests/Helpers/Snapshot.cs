// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Reports;

namespace E2E.Tests.Helpers;

/// <summary>
/// Upstream's <c>tests/helpers/snapshot.ts</c>: builds an <see cref="EngineSnapshot"/> from a flat node list the way tests spell one. A single
/// node is the root itself, several (or none) sit under a stable <c>root</c> node, and the viewport is the fake engine's (1280 by 720).
/// </summary>
public static class Snapshot
{
    /// <summary>The fake engine's viewport.</summary>
    public static ViewportSize Viewport => new() { Width = 1280, Height = 720 };

    /// <summary>Builds a snapshot of <paramref name="nodes"/>.</summary>
    public static EngineSnapshot Of(IReadOnlyList<EngineNode> nodes, string? location = null, ObservationPixels? pixels = null, int? maskedRegionCount = null)
    {
        var root = nodes.Count == 1
            ? nodes[0]
            : new EngineNode(new NodeRef("root", ""), Role: "root", Children: nodes);
        return new EngineSnapshot(root, Viewport)
        {
            Location = location,
            Pixels = pixels,
            MaskedRegionCount = maskedRegionCount,
        };
    }

    /// <summary>A button node with the given id and accessible name, not yet stamped with a revision.</summary>
    public static EngineNode Node(string id, string name) => new(new NodeRef(id, ""), Role: "button", Name: name);

    /// <summary>The operation context the tests run every call with: a one-second budget, never cancelled.</summary>
    public static OperationContext Operation { get; } = new(OperationOrigin.Test)
    {
        Signal = CancellationToken.None,
        Timeout = TimeSpan.FromSeconds(1),
        RunId = "run",
        AttemptId = "attempt",
    };
}
