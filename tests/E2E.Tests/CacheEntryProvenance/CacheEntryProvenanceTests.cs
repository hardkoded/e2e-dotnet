// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Tests.TraceCacheReplay;

namespace E2E.Tests.CacheEntryProvenance;

// .NET-only: upstream has no `it` for a malformed `recordedFor` (its `readProvenance` in cache/trace-read.ts is covered only through
// the integration runs). The port keeps the provenance fields flat on the entry, so `CacheEntry.HasValidProvenance` is checked here.
public sealed class CacheEntryProvenanceTests
{
    [Fact]
    public async Task An_entry_the_port_recorded_replays_with_its_provenance_unchanged()
    {
        var directory = ConfigTests.TempCache();
        await TraceCacheReplayHarness.RunTargetAsync(directory, "web");

        var (step, calls) = await TraceCacheReplayHarness.RunTargetAsync(directory, "web");

        Assert.Equal(0, calls);
        Assert.Equal("self-finalized", step.Cache?.Mode);
    }

    [Theory]
    [InlineData("targetId", "")]
    [InlineData("targetId", "   ")]
    [InlineData("test", "")]
    [InlineData("agent", "")]
    [InlineData("paramsDigest", "ABC")]
    [InlineData("callIndex", -1)]
    public async Task Malformed_provenance_makes_the_entry_a_miss(string field, object value)
    {
        var directory = ConfigTests.TempCache();
        await TraceCacheReplayHarness.RunTargetAsync(directory, "web");
        Edit(directory, field, value);

        var (step, _) = await TraceCacheReplayHarness.RunTargetAsync(directory, "web");

        Assert.Equal("missed", step.Cache?.Mode);
        Assert.Equal("invalid-entry", step.Cache?.Reason);
    }

    [Fact]
    public async Task An_over_long_target_id_makes_the_entry_a_miss()
    {
        var directory = ConfigTests.TempCache();
        await TraceCacheReplayHarness.RunTargetAsync(directory, "web");
        Edit(directory, "targetId", new string('t', 301));

        var (step, _) = await TraceCacheReplayHarness.RunTargetAsync(directory, "web");

        Assert.Equal("invalid-entry", step.Cache?.Reason);
    }

    [Fact]
    public async Task A_non_text_target_id_makes_the_entry_a_miss()
    {
        var directory = ConfigTests.TempCache();
        await TraceCacheReplayHarness.RunTargetAsync(directory, "web");
        Edit(directory, "targetId", 7);

        var (step, _) = await TraceCacheReplayHarness.RunTargetAsync(directory, "web");

        Assert.Equal("invalid-entry", step.Cache?.Reason);
    }

    private static void Edit(string directory, string field, object value)
    {
        var path = Assert.Single(Directory.GetFiles(directory));
        var entry = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        entry[field] = System.Text.Json.JsonSerializer.SerializeToNode(value);
        File.WriteAllText(path, entry.ToJsonString());
    }
}
