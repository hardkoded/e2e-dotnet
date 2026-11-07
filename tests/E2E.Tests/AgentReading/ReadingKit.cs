// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentReading;

/// <summary>The helpers upstream's <c>agent-reading.test.ts</c> keeps at the top of its file: a node, and an observation of nodes.</summary>
internal static class ReadingKit
{
    public static SemanticNode Node(string id, string? role = null, string? name = null, string? text = null, BoundingBox? rect = null) =>
        new() { Ref = id, Role = role, Name = name, Text = text, Rect = rect };

    public static Observation IndexOf(params SemanticNode[] nodes) => new() { Route = "/", Roots = nodes };

    /// <summary>A list item of a page, <paramref name="at"/> slots down from <paramref name="top"/>.</summary>
    public static SemanticNode Slot(string id, string text, int at, int top = 0) =>
        Node(id, role: "listitem", text: text, rect: new BoundingBox(0, top + at * 40, 300, 40));
}
