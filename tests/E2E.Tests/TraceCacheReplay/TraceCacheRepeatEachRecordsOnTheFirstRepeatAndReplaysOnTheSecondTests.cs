// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCacheReplay;

// Upstream's trace-cache-replay.test.ts, describe 'trace cache: --repeat-each records on the first repeat and replays on the second'.
public sealed class TraceCacheRepeatEachRecordsOnTheFirstRepeatAndReplaysOnTheSecondTests
{
    [Fact]
    public async Task Partial_Serves_the_second_repeat_from_the_entry_the_first_one_confirmed()
    {
        // PARTIAL: the port has no runner and no `--repeat-each`, so the two repeats are two sessions of one
        // test over one cache directory. Upstream runs them as one run with two results.
        var directory = ConfigTests.TempCache();

        var (first, firstCalls) = await TraceCacheReplayHarness.RunTargetAsync(directory, null);
        Assert.True(firstCalls > 0);
        Assert.Equal("missed", first.Cache?.Mode);
        Assert.Equal("no-entry", first.Cache?.Reason);

        var (second, calls) = await TraceCacheReplayHarness.RunTargetAsync(directory, null);
        Assert.Equal(0, calls);
        Assert.Equal("self-finalized", second.Cache?.Mode);
        Assert.Equal(0, second.ModelCalls);
        Assert.Equal(1, second.Actions);
        Assert.Single(Directory.GetFiles(directory));
    }
}
