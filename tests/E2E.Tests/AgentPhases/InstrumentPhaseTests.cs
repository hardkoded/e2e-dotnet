// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.AgentPhases;

/// <summary>
/// Upstream's <c>describe('instrumentPhase')</c> (<c>tests/unit/agent-phases.test.ts</c>). The other two <c>describe</c> blocks of that
/// file, <c>retryingObserve</c> and <c>checkStepClock</c>, test functions the port has not extracted.
/// </summary>
public sealed class InstrumentPhaseTests
{
    [Fact]
    public async Task Records_the_engine_code_of_a_failed_phase_not_the_error_class_name()
    {
        var steps = new StepRecorder("attempt");
        try
        {
            await steps.RunAsync(StepKind.Agent, "agent.act", "probe", async () =>
            {
                try
                {
                    await Phases.InstrumentPhaseAsync(
                        new PhaseHost(steps, DebugTrace.Off),
                        new PhaseSpec(StepEventKind.Engine, Phases.Action, "tap"),
                        () => Task.FromException<int>(new EngineException("NOT_ACTIONABLE", "observe: NOT_ACTIONABLE")));
                }
                catch (EngineException)
                {
                }

                throw new InvalidOperationException("end the step");
            });
        }
        catch (InvalidOperationException)
        {
        }

        var step = steps.All()[0];
        var failed = Assert.Single(step.Events, candidate => candidate.Kind == StepEventKind.Engine);
        Assert.Equal(StepEventStatus.Failed, failed.Status);
        Assert.Equal("NOT_ACTIONABLE", failed.Code);
    }
}
