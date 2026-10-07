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
        using var site = await TinySite.StartAsync(GesturesPage);
        var directory = Path.Combine(Path.GetTempPath(), "e2e-verbs", Guid.NewGuid().ToString("n"));
        var model = CheckExpressModel();
        var session = await StartAsync(site, model, new FileStepCache(directory));
        await RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/gestures");
            await session.Agent.ActAsync("pick Express delivery");
            await Expect.That(session.Screen.GetByRole("status", "Gesture state")).ToHaveValueAsync("delivery: Express");
        });

        var actions = RecordedActions(directory);
        Assert.Equal(["check radio Express"], actions.Select(action => action.Kind + " " + action.Role + " " + action.Name));
        var turn = string.Join('\n', model.Requests[1].Messages.Select(message => message.Content));
        Assert.Matches(new Regex("""checked radio "Express"[\s\S]*status "Gesture state"[\s\S]*delivery: Express"""), turn);
    }

}
