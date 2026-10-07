// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using static E2E.Tests.AgentActVerbs.GesturesSession;

namespace E2E.Tests.AgentActVerbs;

/// <summary><c>agent.act</c> grammar verbs recorded on a first run, then replayed with no model call.</summary>
[Collection(BrowserCollection.Name)]
public sealed class AgentActGrammarVerbsRecordThenZeroTurnReplayTests
{
    // Upstream's test replays every verb flow; this commit added the check flow, the one ported here.
    [Fact]
    public async Task Replays_hover_drag_check_upload_and_scroll_into_view_without_a_model_call_and_runs_a_round_trip_that_changed_nothing_live()
    {
        using var site = await TinySite.StartAsync(GesturesPage);
        var directory = Path.Combine(Path.GetTempPath(), "e2e-verbs", Guid.NewGuid().ToString("n"));
        for (var run = 0; run < 2; run++)
        {
            var model = CheckExpressModel();
            var session = await StartAsync(site, model, new FileStepCache(directory));
            await RunAsync(session, async () =>
            {
                await session.App.OpenAsync("/gestures");
                await session.Agent.ActAsync("pick Express delivery");
                await Expect.That(session.Screen.GetByRole("status", "Gesture state")).ToHaveValueAsync("delivery: Express");
            });
            Assert.Equal(run, session.Replayed);
            Assert.Equal(run == 0, model.CallCount > 0);
        }

        Assert.Equal(["check"], RecordedActions(directory).Select(action => action.Kind));
    }
}
