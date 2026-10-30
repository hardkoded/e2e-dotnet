// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AgentTraceCache;

/// <summary>Upstream runs the suite twice through the runner and reads both tests from its report. The port runs the same step in sessions.</summary>
public sealed class TraceCacheDivergenceHandsTheStepOverMidStepTests
{
    [Fact]
    public async Task Hands_the_executor_the_replayed_prefix_and_still_passes()
    {
        var directory = TwoTapCounter.NewDirectory();
        await TwoTapCounter.RecordAndSabotageAsync(directory);

        var model = TwoTapCounter.Model();
        ActResult result;
        await using (var session = await TwoTapCounter.StartAsync(directory, model))
        {
            result = await TwoTapCounter.RunAsync(session);
            session.Complete();
        }

        Assert.Equal(("agent-concluded", "target-not-found", 1, 2), (result.Cache?.Mode, result.Cache?.Reason, result.Cache?.ReplayedActions, result.Cache?.TotalActions));
        Assert.Matches("^at action 2 of 2, ", result.Cache?.Detail);
        var notice = model.Requests[0].Messages[0].Content!;
        Assert.Contains("Cached replay already performed these recorded actions for this step:\n1. tap", notice, StringComparison.Ordinal);
        Assert.Equal(1, TwoTapCounter.Replayed(notice));
        Assert.Equal(2, model.CallCount);
    }

    [Fact]
    public async Task Rewrites_the_entry_on_pass_healing_the_sabotaged_descriptor()
    {
        var directory = TwoTapCounter.NewDirectory();
        await TwoTapCounter.RecordAndSabotageAsync(directory);
        await using (var session = await TwoTapCounter.StartAsync(directory, TwoTapCounter.Model()))
        {
            await TwoTapCounter.RunAsync(session);
            session.Complete();
        }

        var action = TwoTapCounter.OnlyEntry(directory).Actions[1];
        Assert.Equal(("tap", "Increment"), (action.Kind, action.Name));
    }
}
