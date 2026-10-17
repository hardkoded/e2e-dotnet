// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Modes = E2E.Internal.RecordingModes;

namespace E2E.Tests.RecordingModes;

/// <summary>What the engine has to honour before the run starts. Upstream's <c>recordsOnSomeAttempt</c> describe.</summary>
public sealed class RecordsOnSomeAttemptTests
{
    [Fact]
    public void Asks_the_engine_only_when_some_attempt_records()
    {
        Assert.False(Modes.RecordsOnSomeAttempt(RecordingMode.Off, 3));
        Assert.True(Modes.RecordsOnSomeAttempt(RecordingMode.On, 0));
        Assert.True(Modes.RecordsOnSomeAttempt(RecordingMode.RetainOnFailure, 0));
        Assert.False(Modes.RecordsOnSomeAttempt(RecordingMode.OnFirstRetry, 0));
        Assert.True(Modes.RecordsOnSomeAttempt(RecordingMode.OnFirstRetry, 1));
        Assert.False(Modes.RecordsOnSomeAttempt(RecordingMode.OnAllRetries, 0));
        Assert.True(Modes.RecordsOnSomeAttempt(RecordingMode.OnAllRetries, 2));
    }
}
