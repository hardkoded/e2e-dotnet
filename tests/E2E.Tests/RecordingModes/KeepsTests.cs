// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Modes = E2E.Internal.RecordingModes;

namespace E2E.Tests.RecordingModes;

/// <summary>One keep rule for the trace and the video. Upstream's <c>keeps</c> describe.</summary>
public sealed class KeepsTests
{
    [Fact]
    public void Keeps_nothing_of_an_attempt_that_never_ran_and_under_on_failure_everything_that_did_not_pass_interrupted_included()
    {
        AttemptStatus[] statuses = [AttemptStatus.Passed, AttemptStatus.Failed, AttemptStatus.TimedOut, AttemptStatus.Interrupted, AttemptStatus.Skipped];
        Assert.Equal([true, true, true, true, false], statuses.Select(status => Modes.Keeps(Keep.Always, status)));
        Assert.Equal([false, true, true, true, false], statuses.Select(status => Modes.Keeps(Keep.OnFailure, status)));
    }
}
