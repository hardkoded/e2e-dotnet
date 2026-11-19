// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.Executor.ExecutorContextHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('gates')</c> of <c>executor.test.ts</c>.</summary>
public sealed class GatesTests
{
    [Fact]
    public async Task Stays_off_by_default()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, call) =>
                id switch
                {
                    "operation" when call == 0 => new ScriptedAnswer("tap", Spread(keys, "tap", 0.2), 0.9),
                    "operation" => new ScriptedAnswer("done"),
                    "verdict" => new ScriptedAnswer("holds"),
                    _ => new ScriptedAnswer(keys[0]),
                }
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });
        var verdict = await Run(model, fixture);
        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
    }

    [Fact]
    public async Task Blocks_a_low_probability_operation_under_a_gate()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, _) =>
                id == "operation"
                    ? new ScriptedAnswer("tap", Spread(keys, "tap", 0.2), 0.9)
                    : new ScriptedAnswer(keys[0])
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });
        var verdict = await Run(model, fixture, minProbability: 0.9);
        Assert.Equal(StepVerdictStatus.Blocked, verdict.Status);
        Assert.Equal("AUTOMATION_UNSUPPORTED", verdict.ErrorCode);
        Assert.Equal(0, fixture.Actions.Count("tap"));
    }

    [Fact]
    public async Task Blocks_a_low_probability_target_under_a_gate()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, _) =>
                id == "operation"
                    ? new ScriptedAnswer("tap")
                    : new ScriptedAnswer(keys[0], keys.ToDictionary(key => key, _ => 1.0 / keys.Count), 0.9)
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });
        var verdict = await Run(model, fixture, minProbability: 0.9);
        Assert.Equal(StepVerdictStatus.Blocked, verdict.Status);
        Assert.Equal("AUTOMATION_UNSUPPORTED", verdict.ErrorCode);
        Assert.Equal(0, fixture.Actions.Count("tap"));
    }
}
