// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentObservation;

/// <summary>
/// <c>isTransitionalObservation</c>: whether a capture is a screen in transition rather than a screen. Upstream also
/// checks that an empty root whose unproven screenshot was withheld is still in transition; the port's observation keeps the
/// pixels it carries (the model gets them only after the proof, in the feed), so that check is a canvas whose pixels
/// are there and is not in transition.
/// </summary>
public sealed class IsTransitionalObservationTests
{
    [Theory]
    [InlineData("document")]
    [InlineData("screen")]
    [InlineData("window")]
    public void Recognizes_an_empty_root_without_treating_a_lone_control_as_an_empty_screen(string role)
    {
        Assert.True(Transitional(Node(role)));
        Assert.False(Transitional(Node("button", "Continue")));
        Assert.False(Transitional(Node("textbox", "Password", states: new NodeStates { Secure = true })));
        Assert.False(Transitional(Node(role, children: [Node("text", text: "Ready")])));
    }

    [Fact]
    public void Treats_an_empty_root_that_carries_pixels_or_has_no_tree_as_a_screen()
    {
        var empty = new SemanticNode { Ref = "e1", Role = "screen", States = new NodeStates(), Children = [] };
        var canvas = new Observation { Route = "/", Roots = [empty], Pixels = new EngineScreenshot([1], 1, 0) };
        Assert.False(global::E2E.AgentObservation.IsTransitional(canvas));
        Assert.False(global::E2E.AgentObservation.IsTransitional(new Observation { Route = "/", Roots = [], TreeUnavailable = true }));
        Assert.True(global::E2E.AgentObservation.IsTransitional(new Observation { Route = "/", Roots = [empty] }));
    }

    private static bool Transitional(SemanticNode root) => global::E2E.AgentObservation.IsTransitional(new Observation { Route = "/", Roots = [root] });

    private static SemanticNode Node(string role, string? name = null, string? text = null, NodeStates? states = null, List<SemanticNode>? children = null)
    {
        return new SemanticNode { Ref = "e1", Role = role, Name = name, Text = text, States = states ?? new NodeStates(), Children = children ?? [] };
    }
}
