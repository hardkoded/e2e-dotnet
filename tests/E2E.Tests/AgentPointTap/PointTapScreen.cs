// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentPointTap;

/// <summary>The screen of upstream's <c>agent-point-tap.test.ts</c>: a toolbar, a paragraph, a framed button, and a field.</summary>
internal static class PointTapScreen
{
    public static SemanticNode Node(string id, string? role = null, string? name = null, string? text = null, BoundingBox? rect = null, NodeStates? states = null, params SemanticNode[] children) =>
        new() { Ref = id, Role = role, Name = name, Text = text, Rect = rect, States = states ?? new NodeStates(), Children = children };

    public static BoundingBox Rect(double x, double y, double width, double height) => new(x, y, width, height);

    public static PreparedObservation Screen { get; } = global::E2E.Tests.TraceReplay.TestHost.Prepare(Observe(
        Node(
            "root",
            "document",
            rect: Rect(0, 0, 1280, 720),
            children:
            [
                Node(
                    "n1",
                    "region",
                    "Toolbar",
                    rect: Rect(0, 0, 1280, 60),
                    children:
                    [
                        Node("n2", "button", "Save", rect: Rect(10, 10, 100, 40)),
                        Node("n3", "button", "Ghost", rect: Rect(200, 10, 100, 40), states: new NodeStates { Hidden = true }),
                        Node("n4", "button", "Off", rect: Rect(400, 10, 100, 40), states: new NodeStates { Disabled = true }),
                    ]),
                Node("n5", "paragraph", text: "Body copy", rect: Rect(0, 100, 1280, 40)),
                Node("n6", "button", "Framed", rect: Rect(10, 200, 100, 40)),
                Node("n7", "textbox", "Email", rect: Rect(10, 300, 300, 40)),
            ])), global::E2E.Internal.Redactor.None);

    public static Observation Observe(SemanticNode tree) => new() { Route = "/", Roots = [tree], Viewport = new TraceViewport(1280, 720) };

    public static SemanticNode Find(string id) => E2E.LocatorResolver.Walk(Screen.Tree).Single(node => node.Ref == id);
}
