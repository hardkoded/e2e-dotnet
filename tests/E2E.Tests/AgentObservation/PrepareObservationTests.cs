// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentObservation;

public sealed class PrepareObservationTests
{
    /// <summary>The port renders the screen in its own format and marker, and counts no bytes or nodes beside the text.</summary>
    [Fact]
    public void Carries_an_engine_reported_cut_through_as_truncated_with_its_own_marker()
    {
        var tree = new SemanticNode { Ref = "n1", Role = "document", Children = [new SemanticNode { Ref = "n2", Role = "button", Name = "One" }] };
        var text = SnapshotText.Render(new Observation { Route = "/", Roots = [tree], Truncated = true }, []);
        Assert.Contains("button \"One\"", text, StringComparison.Ordinal);
        Assert.EndsWith("\n(More of the page is off screen. Scroll to reach it.)", text, StringComparison.Ordinal);
    }
}
