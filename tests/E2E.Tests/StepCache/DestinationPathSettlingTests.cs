// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.TraceReplay;
using static E2E.Tests.StepCache.StepCacheHarness;

namespace E2E.Tests.StepCache;

public sealed class DestinationPathSettlingTests
{
    /// <summary>Partial port of "self-finalizes when the recorded destination arrives after the replayed action": a replayed step's summary names no zero-turn replay.</summary>
    [Fact]
    public async Task Self_finalizes_when_the_recorded_destination_arrives_late()
    {
        // Start on /pricing; the first read after the replay still sees /pricing (the navigation has not committed),
        // the next sees the destination.
        var host = MakeHost(["/pricing", "/pricing", "/customers"]);
        host.ReplayTimeout = TimeSpan.FromSeconds(5);
        host.Remaining = 60_000;

        var verdict = await MakeSession(EntryContext(endPath: "/customers"), host).BeginAsync();

        Assert.Equal(StepVerdictStatus.Passed, verdict?.Status);
    }

    /// <summary>Upstream's "keeps looking past the backoff for a destination that commits late, up to the path timeout", on a fake clock.</summary>
    [Fact]
    public async Task Keeps_looking_past_the_backoff_for_a_destination_that_commits_late_up_to_the_path_timeout()
    {
        // The destination commits 8 s after the step starts: after the backoff's last listed delay, inside its 15 s bound.
        StepHost Late(AutoAdvanceClock clock, TimeSpan arrives)
        {
            var started = clock.GetUtcNow();
            var host = MakeHost(clock, ["/pricing"]);
            host.Capture = _ => new Observation { Route = clock.GetUtcNow() - started < arrives ? "/pricing" : "/customers", Roots = [] };
            host.ReplayTimeout = TimeSpan.FromSeconds(15);
            host.Remaining = 60_000;
            return host;
        }

        var verdict = await MakeSession(EntryContext(endPath: "/customers"), Late(new AutoAdvanceClock(), TimeSpan.FromSeconds(8))).BeginAsync();
        Assert.Equal(StepVerdictStatus.Passed, verdict?.Status);

        var never = new AutoAdvanceClock();
        var neverStarted = never.GetUtcNow();
        var session = MakeSession(EntryContext(endPath: "/customers"), Late(never, TimeSpan.MaxValue));
        Assert.Null(await session.BeginAsync());
        Assert.Equal(ReplayHandOffReason.EndMismatch, session.ReplayedPrefix?.StopReason);
        Assert.True(never.GetUtcNow() - neverStarted > TimeSpan.FromSeconds(12));
        Assert.True(never.GetUtcNow() - neverStarted <= TimeSpan.FromSeconds(15.1));
    }

    [Fact]
    public async Task Still_hands_off_when_the_destination_never_arrives_within_the_budget()
    {
        var host = MakeHost(["/pricing", "/pricing", "/pricing", "/pricing"]);
        host.ReplayTimeout = TimeSpan.FromMilliseconds(250);
        host.Remaining = 60_000;
        var session = MakeSession(EntryContext(endPath: "/customers"), host);

        Assert.Null(await session.BeginAsync());

        Assert.Equal(ReplayHandOffReason.EndMismatch, session.ReplayedPrefix?.StopReason);
    }
}
