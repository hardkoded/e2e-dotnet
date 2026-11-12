// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using LocatorEngineType = global::E2E.LocatorEngine;

namespace E2E.Tests.LocatorEngine;

/// <summary>Upstream's <c>isNodeVisible</c>.</summary>
public sealed class IsNodeVisibleTests
{
    [Fact]
    public void Treats_missing_nodes_and_hidden_state_correctly()
    {
        Assert.False(LocatorEngineType.IsNodeVisible(null));
        Assert.False(LocatorEngineType.IsNodeVisible(new SemanticNode { Ref = "node-1", Role = "button", Name = "Submit", States = new NodeStates { Hidden = true } }));
        Assert.True(LocatorEngineType.IsNodeVisible(new SemanticNode { Ref = "node-1", Role = "button", Name = "Submit", States = new NodeStates { Hidden = false } }));
        Assert.True(LocatorEngineType.IsNodeVisible(new SemanticNode { Ref = "node-1", Role = "button" }));
    }

    // .NET-only: upstream's hidden state includes a box with no size; the port keeps that state to the render rules and reads EmptyBox here.
    [Fact]
    public void Treats_a_node_with_an_empty_box_as_not_visible()
    {
        Assert.False(LocatorEngineType.IsNodeVisible(new SemanticNode { Ref = "node-1", States = new NodeStates { EmptyBox = true } }));
        Assert.True(LocatorEngineType.IsNodeVisible(new SemanticNode { Ref = "node-1", Rect = new BoundingBox(0, 0, 0, 0) }));
    }
}
