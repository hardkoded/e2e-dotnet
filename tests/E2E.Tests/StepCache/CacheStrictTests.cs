// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.StepCache.StepTraceSessionTests;

namespace E2E.Tests.StepCache;

/// <summary>
/// "still runs live a step with no recording and a recording too long to replay" is not ported: the port has no truncated entries.
/// "fails a step whose recording diverged instead of handing it off, and keeps the cache detail" is covered by <c>ConfigTests.Strict_mode_fails_a_stale_recording</c>.
/// </summary>
public sealed class CacheStrictTests
{
    [Fact]
    public async Task Fails_an_entry_it_cannot_read_and_a_recording_made_on_another_screen()
    {
        var unreadable = await StepAsync(StorageWorld(), new MalformedCache(), "/storage", null, Script(), strict: true);
        Assert.Equal("REPLAY_STALE", Assert.IsType<AgentException>(unreadable.Error).Code);

        var directory = CoreTests.TempCache();
        var world = new DocumentWorld()
            .Map("/pricing", page =>
            {
                var status = page.Status("Marker draft");
                page.Button("Upgrade", () => status.Name = status.Text = "Marker saved");
            })
            .Map("/billing", page => page.Heading("Billing"));
        Assert.Null((await StepAsync(world, new FileStepCache(directory), "/pricing", Saved, Script(ModelResponses.Tap("button", "Upgrade")))).Error);

        var elsewhere = await StepAsync(world, new FileStepCache(directory), "/billing", null, Script(), strict: true);
        var error = Assert.IsType<AgentException>(elsewhere.Error);
        Assert.Equal("REPLAY_STALE", error.Code);
        Assert.Contains("(wrong-context)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_the_stale_entry_it_failed_on_so_the_next_strict_run_fails_on_it_too()
    {
        var directory = CoreTests.TempCache();
        await RecordCustomersAsync(directory);
        var file = Assert.Single(Directory.GetFiles(directory, "*.json"));
        var written = await File.ReadAllTextAsync(file);
        var cache = new CountingCache(directory);

        var first = await StepAsync(CustomersWorld(saved: false), cache, "/pricing", null, Script(), strict: true);
        Assert.Equal("REPLAY_STALE", Assert.IsType<AgentException>(first.Error).Code);
        var second = await StepAsync(CustomersWorld(saved: false), cache, "/pricing", null, Script(), strict: true);
        Assert.Equal("REPLAY_STALE", Assert.IsType<AgentException>(second.Error).Code);

        Assert.Equal((0, 0), (cache.Writes, cache.Deletes));
        Assert.Equal(written, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Runs_live_when_the_store_read_rejects_since_nothing_says_a_recording_exists()
    {
        var step = await StepAsync(StorageWorld(), new ThrowingCache(), "/storage", null, Script(ModelResponses.Tap("button", "Save marker")), strict: true);

        Assert.Null(step.Error);
        Assert.Equal(("missed", "invalid-entry"), (step.Cache?.Mode, step.Cache?.Reason));
    }

    [Fact]
    public async Task Replays_a_recording_that_still_holds_as_it_always_does()
    {
        var directory = CoreTests.TempCache();
        await RecordCustomersAsync(directory);

        var step = await StepAsync(CustomersWorld(), new FileStepCache(directory), "/pricing", null, Script(), strict: true);

        Assert.Null(step.Error);
        Assert.Equal(1, step.Replayed);
        Assert.Equal("self-finalized", step.Cache?.Mode);
    }
}
