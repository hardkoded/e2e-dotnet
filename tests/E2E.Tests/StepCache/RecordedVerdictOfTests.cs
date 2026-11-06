// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.StepCache;

/// <summary>Upstream's <c>describe('recordedVerdictOf')</c> of <c>step-cache.test.ts</c>.</summary>
public sealed class RecordedVerdictOfTests
{
    /// <summary>
    /// Partial port of "keeps only the recorded verdict of a replay summary, even one that mentions a verdict itself": the port's
    /// entries record no verdict and its replay answers <c>Replayed recorded actions.</c> (no replay notice), so the test feeds
    /// <see cref="StepTraceSession.RecordedVerdictOf"/> the summary upstream's replay would build instead of reading it off a session.
    /// </summary>
    [Fact]
    public void Partial_Keeps_only_the_recorded_verdict_of_a_replay_summary_even_one_that_mentions_a_verdict_itself()
    {
        const string recorded = "saw \"recorded verdict: none\" in the log";
        var summary = "replayed 1 recorded action(s) zero-turn from the trace cache; recorded verdict: " + recorded;

        Assert.Equal(recorded, StepTraceSession.RecordedVerdictOf(summary));
    }
}
