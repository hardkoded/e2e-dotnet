// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AgentObservation;

/// <summary>
/// <c>interactiveNodeCount</c>: how many listed nodes the model could act on, read off the rendered lines. The lines are the port's
/// (<c>- role "name" [ref=e1]</c>), where upstream's are <c>#n1 role "name"</c>.
/// </summary>
public sealed class InteractiveNodeCountTests
{
    [Fact]
    public void Counts_the_listed_nodes_the_model_could_act_on()
    {
        Assert.Equal(
            3,
            global::E2E.AgentObservation.InteractiveNodeCount(string.Join(
                '\n',
                "Screen (/):",
                "- document \"Home\" [ref=e1]",
                "  - heading \"Welcome\" [ref=e2]",
                "  - button \"Increment\" [ref=e3]",
                "  - status \"Counter\" text=\"0\" [ref=e4]",
                "  - link \"About\" [ref=e5]",
                "  - textbox \"Email\" [ref=e6]")));
        Assert.Equal(
            0,
            global::E2E.AgentObservation.InteractiveNodeCount(string.Join('\n', "Screen (/):", "- document \"Canvas\" [ref=e1]", "  - heading \"Map\" [ref=e2]", "  - status \"Picked\" [ref=e3]")));
    }
}
