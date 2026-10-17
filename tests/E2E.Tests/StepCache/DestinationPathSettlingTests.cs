// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

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

        var verdict = await MakeSession(EntryContext(endPath: "/customers"), host).BeginAsync();

        Assert.Equal(StepVerdictStatus.Passed, verdict?.Status);
    }

    [Fact]
    public async Task Still_hands_off_when_the_destination_never_arrives_within_the_budget()
    {
        var host = MakeHost(["/pricing", "/pricing", "/pricing", "/pricing"]);
        host.ReplayTimeout = TimeSpan.FromMilliseconds(250);
        var session = MakeSession(EntryContext(endPath: "/customers"), host);

        Assert.Null(await session.BeginAsync());

        Assert.Equal(ReplayHandOffReason.EndMismatch, session.ReplayedPrefix?.StopReason);
    }
}
