// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.TraceReplay.TestHost;

namespace E2E.Tests.TraceReplay;

/// <summary>
/// <c>describe('verifyEndState')</c> of <c>trace-replay.test.ts</c>, against <c>Replay.VerifyEndState</c> and a <see cref="TestHost"/>
/// on a clock that sleeps at once (<see cref="AutoAdvanceClock"/>, upstream's fake timers). Upstream's <c>verifyAnchors</c> helper
/// reads the recorded end anchors with <c>deltaMismatch</c>; the port's entry records the nodes that appeared and went, which
/// <c>Anchors.Mismatch</c> reads.
/// </summary>
public sealed class VerifyEndStateTests
{
    private static readonly SemanticNode Saved = new() { Ref = "m", Role = "status", Name = "Marker", Text = "saved" };
    private static readonly RecordedTarget SavedAnchor = new() { Role = "status", Name = "Marker", Text = "saved" };
    private static readonly RecordedTarget UpgradeAnchor = new() { Role = "button", Name = "Upgrade" };

    private static Task<bool> VerifyAnchors(TestHost host, IReadOnlyList<RecordedTarget> endAnchors, int? waitMs = null)
    {
        var entry = new CacheEntry { Appeared = [.. endAnchors] };
        return Replay.VerifyEndState(
            host,
            screen => Anchors.Mismatch(entry, Anchors.Project(screen, _ => false, text => text, text => text), []) is null,
            new VerifyEndStateOptions { WaitMs = waitMs });
    }

    /// <summary>A host with <paramref name="remainingMs"/> on the step clock whose screen shows the saved marker from <paramref name="after"/> on.</summary>
    private static TestHost SavedAfter(TimeSpan after, int remainingMs)
    {
        var host = MakeHost();
        host.Time = new AutoAdvanceClock();
        host.ReplayTimeout = TimeSpan.FromSeconds(15);
        host.Remaining = remainingMs;
        var from = after == TimeSpan.MaxValue ? DateTimeOffset.MaxValue : host.Time.GetUtcNow() + after;
        host.Capture = () => Screen(host.Time.GetUtcNow() >= from ? [Upgrade, Saved] : [Upgrade]);
        return host;
    }

    private static TestHost Fast(IReadOnlyList<SemanticNode>? nodes = null, int? remainingMs = null)
    {
        var host = MakeHost(nodes);
        host.Time = new AutoAdvanceClock();
        host.ReplayTimeout = TimeSpan.FromSeconds(15);
        host.Remaining = remainingMs ?? host.Remaining;
        return host;
    }

    [Fact]
    public async Task Holds_when_every_anchor_is_present_counting_an_ambiguous_match_as_presence()
    {
        var twin = new SemanticNode { Ref = "m2", Role = "status", Name = "Marker", Text = "saved" };
        var host = Fast([Upgrade, Saved, twin]);

        Assert.True(await VerifyAnchors(host, [SavedAnchor, UpgradeAnchor]));
    }

    [Fact]
    public async Task Fails_when_any_anchor_is_missing_once_the_clock_leaves_no_room_to_wait()
    {
        var host = Fast([Upgrade, Email], remainingMs: 50);

        Assert.False(await VerifyAnchors(host, [UpgradeAnchor, SavedAnchor]));
    }

    [Fact]
    public async Task Treats_a_surface_that_cannot_be_observed_as_a_mismatch_never_as_a_step_failure()
    {
        var host = Fast();
        host.Capture = () => throw new EngineException(EngineErrorCodes.InvalidState, "no surface to observe");

        Assert.False(await VerifyAnchors(host, [SavedAnchor]));
    }

    [Fact]
    public async Task Rethrows_a_runtime_hard_stop_raised_while_looking()
    {
        var host = Fast();
        host.Capture = () => throw new AgentException("STEP_TIMEOUT", "out of time");

        var error = await Assert.ThrowsAsync<AgentException>(() => VerifyAnchors(host, [SavedAnchor]));

        Assert.Equal("STEP_TIMEOUT", error.Code);
    }

    [Fact]
    public async Task Waits_out_a_slow_effect_before_giving_up()
    {
        var host = Fast();
        var shown = false;
        host.Capture = () =>
        {
            host.Observations++;
            // Present from the third look on: the effect landed after the last action.
            shown = host.Observations >= 3;
            return Screen(shown ? [Upgrade, Saved] : [Upgrade]);
        };

        Assert.True(await VerifyAnchors(host, [SavedAnchor]));
        Assert.True(shown);
    }

    [Fact]
    public async Task Waits_past_the_settling_backoff_for_an_effect_the_recording_waited_longer_for_while_the_step_clock_keeps_its_reserve()
    {
        var late = SavedAfter(TimeSpan.FromSeconds(20), 120_000);
        var started = late.Time.GetUtcNow();
        Assert.True(await VerifyAnchors(late, [SavedAnchor], 30_000));
        Assert.Equal(TimeSpan.FromSeconds(20), late.Time.GetUtcNow() - started);

        Assert.False(await VerifyAnchors(SavedAfter(TimeSpan.FromSeconds(20), 25_000), [SavedAnchor], 30_000));
    }

    /// <summary>Partial: the port's capture is always semantic, so the surface turning to pixels, which ends the wait at once, is not checked.</summary>
    [Fact]
    public async Task Partial_gives_up_the_long_end_wait_when_it_runs_out_or_at_once_when_the_surface_turns_to_pixels()
    {
        var host = SavedAfter(TimeSpan.MaxValue, 120_000);
        var started = host.Time.GetUtcNow();

        Assert.False(await VerifyAnchors(host, [SavedAnchor], 30_000));
        Assert.Equal(TimeSpan.FromSeconds(30), host.Time.GetUtcNow() - started);
    }

    [Fact]
    public async Task Surfaces_a_cancelled_wait_as_a_cancellation_never_as_a_mismatch()
    {
        using var cancel = new CancellationTokenSource();
        var host = SavedAfter(TimeSpan.MaxValue, 120_000);
        host.Signal = cancel.Token;
        host.Capture = () =>
        {
            cancel.Cancel();
            return Screen([Upgrade]);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => VerifyAnchors(host, [SavedAnchor], 30_000));
    }
}
