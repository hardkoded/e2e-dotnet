// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCacheReplay;

// Upstream's trace-cache-replay.test.ts, describe 'trace cache: two targets keep separate entries'.
// The port has no runner, so each target is one session over the shared cache directory.
public sealed class TraceCacheTwoTargetsKeepSeparateEntriesTests
{
    [Fact]
    public async Task Partial_Records_one_entry_per_target_and_lists_both()
    {
        // PARTIAL: the port has no `e2e cache ls`, and an entry does not record its target (upstream's
        // `recordedFor.targetId`), so the test shows the separation by count: the second target misses and writes
        // its own file, since the target is part of the key every file name hashes.
        var directory = ConfigTests.TempCache();
        foreach (var target in new[] { "web", "web-b" })
        {
            var (result, calls) = await TraceCacheReplayHarness.RunTargetAsync(directory, target);
            Assert.True(calls > 0);
            Assert.Equal("missed", result.Cache?.Mode);
            Assert.Equal("no-entry", result.Cache?.Reason);
        }

        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }

    [Fact]
    public async Task Replays_on_both_targets_without_a_model_call()
    {
        var directory = ConfigTests.TempCache();
        foreach (var target in new[] { "web", "web-b" })
        {
            await TraceCacheReplayHarness.RunTargetAsync(directory, target);
        }

        foreach (var target in new[] { "web", "web-b" })
        {
            var (step, calls) = await TraceCacheReplayHarness.RunTargetAsync(directory, target);
            Assert.Equal(0, calls);
            Assert.Equal("self-finalized", step.Cache?.Mode);
            Assert.Equal(0, step.ModelCalls);
            Assert.Equal(1, step.Actions);
        }

        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }
}
