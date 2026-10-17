// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Modes = E2E.Internal.RecordingModes;

namespace E2E.Tests.RecordingModes;

/// <summary>Which attempts keep a trace or a video under each mode. Upstream's <c>attemptKeep</c> describe.</summary>
public sealed class AttemptKeepTests
{
    [Fact]
    public void Keeps_every_attempt_for_on_only_failures_for_retain_on_failure_on_every_attempt()
    {
        Assert.Equal([Keep.Always, Keep.Always, Keep.Always], new[] { 0, 1, 2 }.Select(index => Modes.AttemptKeep(RecordingMode.On, index)));
        Assert.Equal([Keep.OnFailure, Keep.OnFailure], new[] { 0, 3 }.Select(index => Modes.AttemptKeep(RecordingMode.RetainOnFailure, index)));
    }

    [Fact]
    public void Captures_nothing_for_off_only_the_first_retry_for_on_first_retry_and_every_retry_for_on_all_retries()
    {
        Assert.Null(Modes.AttemptKeep(RecordingMode.Off, 0));
        Assert.Equal([null, Keep.Always, null], new[] { 0, 1, 2 }.Select(index => Modes.AttemptKeep(RecordingMode.OnFirstRetry, index)));
        Assert.Equal([null, Keep.Always, Keep.Always], new[] { 0, 1, 2 }.Select(index => Modes.AttemptKeep(RecordingMode.OnAllRetries, index)));
    }
}
