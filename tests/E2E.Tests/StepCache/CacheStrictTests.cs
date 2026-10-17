// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.StepCache.StepCacheHarness;

namespace E2E.Tests.StepCache;

/// <summary>
/// Upstream's <c>describe('cache.strict')</c> at the session seam. "still runs live a step with no recording and a recording too
/// long to replay" is ported without the truncated half, under another name: the port has no truncated entries.
/// </summary>
public sealed class CacheStrictTests
{
    // A strict context is read-only whatever the configured mode.
    private static CacheContext Strict(CacheContext context) => context.Under(strict: true);

    [Fact]
    public async Task Fails_a_step_whose_recording_diverged_instead_of_handing_it_off_and_keeps_the_cache_detail()
    {
        var context = Strict(EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]));
        var session = MakeSession(context, MakeHost(["/pricing", "/customers"]));

        var error = await Assert.ThrowsAsync<AgentException>(session.BeginAsync);

        Assert.Equal("REPLAY_STALE", error.Code);
        Assert.Equal(("agent-concluded", "end-mismatch"), (session.CacheInfo?.Mode, session.CacheInfo?.Reason));
    }

    [Fact]
    public async Task Fails_an_entry_it_cannot_read_and_a_recording_made_on_another_screen()
    {
        var malformed = new CacheEntry { Schema = FileStepCache.SchemaVersion, Actions = [] };
        var unreadable = MakeSession(Strict(FakeContext(() => new CacheLookup { Entry = malformed })), MakeHost(["/"]));
        Assert.Equal("REPLAY_STALE", (await Assert.ThrowsAsync<AgentException>(unreadable.BeginAsync)).Code);

        var elsewhere = MakeSession(Strict(EntryContext(startPath: "/pricing", actions: [Tap("button", "Upgrade")])), MakeHost(["/billing"]));
        Assert.Equal("REPLAY_STALE", (await Assert.ThrowsAsync<AgentException>(elsewhere.BeginAsync)).Code);
        Assert.Equal(("missed", "wrong-context"), (elsewhere.CacheInfo?.Mode, elsewhere.CacheInfo?.Reason));
    }

    [Fact]
    public async Task Runs_live_a_step_with_no_recording()
    {
        var session = MakeSession(Strict(FakeContext(() => new CacheLookup { Reason = "no-entry" })), MakeHost(["/"]));

        Assert.Null(await session.BeginAsync());
    }

    [Fact]
    public async Task Keeps_the_stale_entry_it_failed_on_so_the_next_strict_run_fails_on_it_too()
    {
        var context = Strict(EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]));
        var session = MakeSession(context, MakeHost(["/pricing", "/customers"]));

        await Assert.ThrowsAsync<AgentException>(session.BeginAsync);
        await session.ConcludeAsync(StepOutcome.Failed);

        Assert.Empty(context.Deleted);
    }

    [Fact]
    public async Task Runs_live_when_the_store_read_rejects_since_nothing_says_a_recording_exists()
    {
        var session = MakeSession(Strict(FakeContext(() => throw new InvalidOperationException("redis is down"))), MakeHost(["/"]));

        Assert.Null(await session.BeginAsync());

        Assert.Equal(("missed", "invalid-entry"), (session.CacheInfo?.Mode, session.CacheInfo?.Reason));
    }

    [Fact]
    public async Task Replays_a_recording_that_still_holds_as_it_always_does()
    {
        var context = Strict(EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]));

        var verdict = await MakeSession(context, MakeHost(["/pricing", "/customers"], [SavedMarker])).BeginAsync();

        Assert.Equal(StepVerdictStatus.Passed, verdict?.Status);
    }
}
