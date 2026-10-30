// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using ResultStatus = E2E.Reports.ResultStatus;

namespace E2E.Tests.Retry;

/// <summary>Ports the <c>retryVerdict</c> describe of <c>retry.test.ts</c>.</summary>
public sealed class RetryVerdictTests
{
    private static RetryAttempt Of(AttemptStatus status) => new(status);

    [Fact]
    public void Reads_attempts_as_a_verdict()
    {
        var failed = new RetryAttempt(AttemptStatus.Failed, new SerializedError("test", "X", "x", false, null));
        (RetryAttempt[] Attempts, ResultStatus Verdict)[] cases =
        [
            ([], ResultStatus.Failed),
            ([Of(AttemptStatus.Passed)], ResultStatus.Passed),
            ([failed, Of(AttemptStatus.Passed)], ResultStatus.Flaky),
            ([failed, Of(AttemptStatus.TimedOut)], ResultStatus.TimedOut),
            ([Of(AttemptStatus.Interrupted)], ResultStatus.Interrupted),
            ([Of(AttemptStatus.TimedOut), Of(AttemptStatus.Interrupted)], ResultStatus.TimedOut),
            ([failed, Of(AttemptStatus.Skipped)], ResultStatus.Skipped),
        ];

        foreach (var (attempts, verdict) in cases)
        {
            Assert.Equal(verdict, E2E.Retry.RetryVerdict(attempts));
        }
    }
}
