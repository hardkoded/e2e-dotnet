// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.TraceReplay.TestHost;

namespace E2E.Tests.TraceReplay;

/// <summary>
/// .NET-only: no upstream <c>it</c> covers the order of an upload's authorization and its action slot in a replay. Upstream's
/// <c>upload</c> authorizes the paths before <c>commitTargeted</c> takes the slot and calls <c>onBeforeAction</c>.
/// </summary>
public sealed class ReplayTraceUploadTests
{
    private static RecordedAction Upload { get; } = new() { Kind = "upload", Role = "button", Name = "Upgrade", Paths = ["fixtures/a.txt"] };

    [Fact]
    public async Task Authorizes_a_replayed_upload_before_it_takes_the_slot_and_runs_the_hook()
    {
        var host = MakeHost();

        var outcome = await Replay.ReplayTrace(host, [Upload]);

        Assert.True(outcome.Completed);
        Assert.Equal(["authorize", "reserve", "before"], host.Gates);
        Assert.Equal(["upload"], host.Calls);
    }

    [Fact]
    public async Task A_refused_replayed_upload_takes_no_slot_and_runs_no_hook()
    {
        var host = MakeHost();
        host.UploadPolicy = _ => throw new AgentException("POLICY_DENIED", "\"fixtures/a.txt\" is refused.");

        var outcome = await Replay.ReplayTrace(host, [Upload]);

        Assert.False(outcome.Completed);
        Assert.Equal(ReplayHandOffReason.ActionFailed, outcome.StopReason);
        Assert.Equal(["authorize"], host.Gates);
        Assert.Empty(host.Calls);
    }
}
