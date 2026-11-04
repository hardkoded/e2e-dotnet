// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AgentObservation;

/// <summary><c>describe('interactiveNodeCount')</c> of <c>agent-observation.test.ts</c>.</summary>
public sealed class InteractiveNodeCountTests
{
    [Fact]
    public void Counts_the_listed_nodes_the_model_could_act_on_by_id()
    {
        Assert.Equal(
            3,
            global::E2E.AgentObservation.InteractiveNodeCount(string.Join(
                '\n',
                "#n1 document \"Home\"",
                " #n2 heading \"Welcome\"",
                " #n3 button \"Increment\"",
                " #n4 status \"Counter\" text=\"0\"",
                " #n5 link \"About\"",
                " #n6 textbox \"Email\"")));
        Assert.Equal(
            0,
            global::E2E.AgentObservation.InteractiveNodeCount(string.Join('\n', "#n1 document \"Canvas\"", " #n2 heading \"Map\"", " #n3 status \"Picked\"")));
    }
}
