// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Reports;

/// <summary>
/// What one result left behind (upstream's <c>Outcome</c>, <c>report/outcome.ts</c>). The port has no serial groups, so a result always
/// answers from its own attempts.
/// </summary>
/// <param name="DurationMs">The verdict attempt's duration.</param>
/// <param name="Final">The final attempt: the verdict of a failed test, the passing retry of a flaky one.</param>
/// <param name="Verdict">
/// The last attempt that reached the result's verdict, so a retry the run interrupted does not stand for it; otherwise the final one.
/// </param>
/// <param name="LastFailed">The last attempt before the final one that did not pass, when there was one: what a flaky test hit.</param>
/// <param name="Attempts">Every attempt in order, so a page can say whether they failed alike.</param>
/// <param name="FailedAttempts">Attempts that did not pass before the final one; what a flaky pass cost.</param>
internal sealed record Outcome(long DurationMs, ReportAttempt Final, ReportAttempt Verdict, ReportAttempt? LastFailed, IReadOnlyList<ReportAttempt> Attempts, int FailedAttempts)
{
    /// <summary>What a result that never ran an attempt reads as.</summary>
    private static readonly ReportAttempt NoAttempt = new() { Status = AttemptStatus.Skipped };

    /// <summary>Upstream's <c>outcome()</c>.</summary>
    public static Outcome Of(ReportResult result)
    {
        var earlier = result.Attempts.Take(Math.Max(0, result.Attempts.Count - 1)).ToList();
        var final = result.Attempts.LastOrDefault() ?? NoAttempt;
        var verdict = result.Attempts.LastOrDefault(attempt => ReachedVerdict(result.Status, attempt.Status)) ?? final;
        return new Outcome(
            verdict.DurationMs,
            final,
            verdict,
            earlier.LastOrDefault(attempt => attempt.Status != AttemptStatus.Passed),
            result.Attempts,
            earlier.Count(attempt => attempt.Status != AttemptStatus.Passed));
    }

    /// <summary>Upstream's <c>reachedVerdict</c>: whether an attempt ended the way the result did; a failure and a timeout are one verdict.</summary>
    private static bool ReachedVerdict(ResultStatus result, AttemptStatus attempt) =>
        result is ResultStatus.Failed or ResultStatus.TimedOut
            ? attempt is AttemptStatus.Failed or AttemptStatus.TimedOut
            : result.ToString() == attempt.ToString();
}
