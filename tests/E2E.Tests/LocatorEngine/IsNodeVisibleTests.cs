// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Locators = global::E2E.LocatorEngine;

namespace E2E.Tests.LocatorEngine;

/// <summary>Upstream's <c>isNodeVisible</c>.</summary>
public sealed class IsNodeVisibleTests
{
    [Fact]
    public void Treats_missing_nodes_and_hidden_state_correctly()
    {
        Assert.False(Locators.IsNodeVisible(null));
        Assert.False(Locators.IsNodeVisible(new SemanticNode { Ref = "node-1", Role = "button", Name = "Submit", States = new NodeStates { Hidden = true } }));
        Assert.True(Locators.IsNodeVisible(new SemanticNode { Ref = "node-1", Role = "button", Name = "Submit", States = new NodeStates { Hidden = false } }));
        Assert.True(Locators.IsNodeVisible(new SemanticNode { Ref = "node-1", Role = "button" }));
    }
}
