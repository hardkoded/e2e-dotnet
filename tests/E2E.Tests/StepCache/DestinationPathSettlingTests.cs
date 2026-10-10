// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.StepCache.StepTraceSessionTests;

namespace E2E.Tests.StepCache;

/// <summary>"self-finalizes when the recorded destination arrives after the replayed action" is covered by <c>CoreTests.Replay_waits_for_an_end_state_that_shows_up_late</c>.</summary>
public sealed class DestinationPathSettlingTests
{
    [Fact]
    public async Task Still_hands_off_when_the_destination_never_arrives_within_the_budget()
    {
        var directory = CoreTests.TempCache();
        await StepAsync(ProjectWorld("/customers"), new FileStepCache(directory), "/pricing", Saved, Script(ModelResponses.Tap("link", "Create project")));

        // The link now stays on the pricing page, so the recorded destination never shows up.
        var stays = new DocumentWorld().Map("/pricing", page => page.Link("Create project", "/pricing"));
        var step = await StepAsync(stays, new FileStepCache(directory), "/pricing", null, Script());

        Assert.Null(step.Error);
        Assert.Equal(0, step.Replayed);
        Assert.Equal(("agent-concluded", "end-mismatch"), (step.Cache?.Mode, step.Cache?.Reason));
    }
}
