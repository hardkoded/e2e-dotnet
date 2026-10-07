// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCacheReplay;

/// <summary>Upstream runs a suite of flows through the runner twice. The port runs one flow in two sessions.</summary>
public sealed class TraceCacheEveryFlowRecordsOnTheFirstRunAndReplaysWithoutAModelOnTheSecondTests
{
    [Fact]
    public async Task Replays_every_step_on_the_second_run_with_zero_model_calls_across_the_whole_run()
    {
        var directory = SessionTests.TempCache();
        var firstCalls = 0;
        await using (var session = await SessionTests.StartAsync(directory, () => firstCalls++, attempt: 1))
        {
            await SessionTests.UpgradeAsync(session);
            session.Complete();
            Assert.True(firstCalls > 0);
        }

        var secondCalls = 0;
        await using (var session = await SessionTests.StartAsync(directory, () => secondCalls++, attempt: 1))
        {
            await SessionTests.UpgradeAsync(session);
            session.Complete();
            Assert.Equal(0, secondCalls);
            Assert.Equal(1, session.Replayed);
        }
    }
}
