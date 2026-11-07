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
        // PARTIAL: the port has no `e2e cache ls`, so the listing assertions (the test and the target in one row each) are
        // not ported. The entries are read from the files instead, for the target each records (`recordedFor.targetId`).
        var directory = ConfigTests.TempCache();
        foreach (var target in new[] { "web", "web-b" })
        {
            var (result, calls) = await TraceCacheReplayHarness.RunTargetAsync(directory, target);
            Assert.True(calls > 0);
            Assert.Equal("missed", result.Cache?.Mode);
            Assert.Equal("no-entry", result.Cache?.Reason);
        }

        var recorded = Directory.GetFiles(directory)
            .Select(path => TargetOf(File.ReadAllText(path)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["web", "web-b"], recorded);
    }

    private static string TargetOf(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("targetId", out var target) ? target.GetString() ?? "" : "";
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
