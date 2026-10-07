// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E;
using static E2E.Tests.AgentActVerbs.GesturesSession;

namespace E2E.Tests.AgentActVerbs;

/// <summary><c>agent.act</c> grammar verbs on the gestures page, driven by a scripted model.</summary>
[Collection(BrowserCollection.Name)]
public sealed class AgentActGrammarVerbsTests
{
    [Fact]
    public async Task Records_a_check_whose_radio_the_pick_replaced_the_click_landed()
    {
        using var site = await StartSiteAsync();
        var directory = Path.Combine(Path.GetTempPath(), "e2e-verbs", Guid.NewGuid().ToString("n"));
        var flow = ReplayFlows.Single(entry => entry.Title == "checks a radio the pick replaces with its summary");
        var model = ModelFor(flow);
        await RunFlowAsync(site, flow, model, directory);

        // The port records actions, not engine events with a detail line.
        var actions = RecordedActions(directory, flow.Title);
        Assert.Equal(["check radio Express"], actions.Select(action => action.Kind + " " + action.Role + " " + action.Name));
        // The port's tool result is the action and the screen after it, not a list of changes.
        var turn = string.Join('\n', model.Requests[1].Messages.Select(message => message.Content));
        Assert.Matches(new Regex("""checked radio "Express"[\s\S]*status "Gesture state"[^\n]*value="delivery: Express"""), turn);
    }
}
