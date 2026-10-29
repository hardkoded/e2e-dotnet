// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentPhases;

/// <summary>Upstream's <c>describe('retryingObserve')</c> (<c>tests/unit/agent-phases.test.ts</c>).</summary>
public sealed class RetryingObserveTests
{
    private static readonly Observation Screen = new() { Route = "/", Roots = [] };

    // Upstream's foreignEngineError builds an error as an engine loaded from a config file throws it, not an instance of the harness's
    // class. A .NET engine has one type registry, so the closest equivalent is the engine's own error with that code and retryability.
    private static EngineException EngineError(string code, bool retryable) => new(code, "observe: " + code, retryable);

    [Fact]
    public async Task Partial_Re_reads_after_a_retryable_race_reported_by_an_engine_from_another_module_registry()
    {
        var attempts = 0;
        var observation = await Phases.RetryingObserveAsync(
            () =>
            {
                attempts++;
                return attempts < 3 ? Task.FromException<Observation>(EngineError("NODE_STALE", true)) : Task.FromResult(Screen);
            },
            _ => { },
            new CancellationTokenSource().Token);
        Assert.Same(Screen, observation);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Partial_Surfaces_a_non_retryable_engine_failure_at_once()
    {
        var attempts = 0;
        var thrown = await Assert.ThrowsAsync<EngineException>(() => Phases.RetryingObserveAsync(
            () =>
            {
                attempts++;
                return Task.FromException<Observation>(EngineError("OPERATION_TIMEOUT", false));
            },
            _ => { },
            new CancellationTokenSource().Token));
        Assert.Contains("OPERATION_TIMEOUT", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Partial_Reports_the_step_clock_when_the_capture_timed_out_at_the_deadline()
    {
        var deadline = new Deadline(TimeSpan.FromMilliseconds(50), TimeProvider.System);
        var thrown = await Assert.ThrowsAsync<AgentException>(() => Phases.RetryingObserveAsync(
            () => Task.FromException<Observation>(EngineError("OPERATION_TIMEOUT", false)),
            cause => Phases.CheckStepClock(new CancellationTokenSource().Token, deadline, "agent.act", 3_000, cause),
            new CancellationTokenSource().Token));
        Assert.Equal("STEP_TIMEOUT", thrown.Code);
    }
}
