// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.StepCache;

public sealed class FailedStepOutcomeTests
{
    [Fact]
    public void Reads_a_cancellation_and_a_model_that_never_answered_as_no_verdict_and_every_other_failure_as_failed()
    {
        foreach (var code in new[] { "CANCELLED", "MODEL_PROVIDER_FAILED", "MODEL_UNAVAILABLE" })
        {
            Assert.Equal(StepOutcome.NoVerdict, StepTraceSession.FailedStepOutcome(new AgentException(code, "x")));
        }

        foreach (var code in new[] { "ASSERTION_FAILED", "STEP_TIMEOUT", "MODEL_OUTPUT_INVALID", "CONTEXT_OVERFLOW", "APP_UNREACHABLE", "MODEL_REFUSED" })
        {
            Assert.Equal(StepOutcome.Failed, StepTraceSession.FailedStepOutcome(new AgentException(code, "x")));
        }

        Assert.Equal(StepOutcome.Failed, StepTraceSession.FailedStepOutcome(new InvalidOperationException("MODEL_PROVIDER_FAILED")));
    }

    /// <summary>.NET-only: a stale strict replay writes nothing, so it implicates no entry, and the attempt keeps its cache.</summary>
    [Fact]
    public void Reads_a_stale_strict_replay_as_no_verdict()
    {
        Assert.Equal(StepOutcome.NoVerdict, StepTraceSession.FailedStepOutcome(new AgentException("REPLAY_STALE", "x")));
        Assert.True(AttemptScope.KeepsCache(new AgentException("REPLAY_STALE", "x")));
        Assert.False(AttemptScope.KeepsCache(null));
    }
}
