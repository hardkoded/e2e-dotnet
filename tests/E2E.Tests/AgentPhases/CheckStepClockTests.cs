// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Tests.AgentPhases;

/// <summary>Upstream's <c>describe('checkStepClock')</c> (<c>tests/unit/agent-phases.test.ts</c>).</summary>
public sealed class CheckStepClockTests
{
    private static EngineException EngineError(string code, bool retryable) => new(code, "observe: " + code, retryable);

    // A fixed clock, so a pause of the test thread cannot move a deadline.
    private static readonly FakeTimeProvider Clock = new();

    private static Action Check(Deadline deadline, Exception cause) =>
        () => Phases.CheckStepClock(CancellationToken.None, deadline, "agent.waitFor", 3_000, cause, Clock);

    private static Deadline In(int ms) => new(TimeSpan.FromMilliseconds(ms), Clock);

    private static Exception? ThrownBy(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            return error;
        }

        return null;
    }

    [Fact]
    public void Partial_Reads_an_operation_timeout_within_a_poll_tick_of_the_deadline_as_the_deadline_with_the_clock_still_short_of_it()
    {
        var cause = EngineError("OPERATION_TIMEOUT", false);
        var error = Assert.IsType<AgentException>(ThrownBy(Check(In(50), cause)));
        Assert.Equal("STEP_TIMEOUT", error.Code);
        Assert.Equal("agent.waitFor exceeded its 3000 ms timeout", error.Message);
        Assert.Same(cause, error.InnerException);
    }

    [Fact]
    public void Partial_Leaves_an_operation_timeout_with_time_to_spare_and_any_other_failure_to_the_caller()
    {
        Assert.Null(ThrownBy(Check(In(5_000), EngineError("OPERATION_TIMEOUT", false))));
        Assert.Null(ThrownBy(Check(In(50), EngineError("ENGINE_FAILURE", false))));
    }
}
