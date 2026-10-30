// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using ResultStatus = E2E.Reports.ResultStatus;

namespace E2E;

/// <summary>The status-and-error slice of an attempt the retry policy inspects (upstream's <c>RetryAttempt</c>, <c>run/retry.ts</c>).</summary>
/// <param name="Status">How the attempt ended.</param>
/// <param name="Error">What stopped the attempt, when something did.</param>
internal sealed record RetryAttempt(AttemptStatus Status, SerializedError? Error = null);

/// <summary>
/// The retry policy of a test (upstream's <c>run/retry.ts</c>). The port has only <see cref="RetryVerdict"/>: it has no
/// <c>runWithRetries</c>, <c>isRetryEligible</c>, or <c>isCutSerialRetry</c>, because a test framework runs each retry and the port has no serial groups.
/// </summary>
internal static class Retry
{
    /// <summary>
    /// Upstream's <c>retryVerdict</c>: the verdict a sequence of finished attempts reaches. A pass is <c>flaky</c> after an attempt before it, an interrupt
    /// during a retry keeps the verdict before it, and a skip decides.
    /// </summary>
    public static ResultStatus RetryVerdict(IReadOnlyList<RetryAttempt> attempts)
    {
        var verdict = ResultStatus.Failed;
        for (var index = 0; index < attempts.Count; index++)
        {
            switch (attempts[index].Status)
            {
                case AttemptStatus.Passed:
                    return index > 0 ? ResultStatus.Flaky : ResultStatus.Passed;
                case AttemptStatus.Interrupted:
                    return index > 0 ? verdict : ResultStatus.Interrupted;
                case AttemptStatus.Skipped:
                    return ResultStatus.Skipped;
                case AttemptStatus.TimedOut:
                    verdict = ResultStatus.TimedOut;
                    break;
                default:
                    verdict = ResultStatus.Failed;
                    break;
            }
        }

        return verdict;
    }
}
