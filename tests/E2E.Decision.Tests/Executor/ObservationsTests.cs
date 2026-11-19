// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.Executor.ExecutorContextHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('observations')</c> of <c>executor.test.ts</c>.</summary>
public sealed class ObservationsTests
{
    [Fact]
    public async Task Acts_on_a_truncated_observation()
    {
        var (model, _) = ScriptedDecision((id, keys, call) => Pick(id, keys, call == 0 ? "tap" : "done"));
        var fixture = Context(new ContextOptions { Tree = Buttons, Observation = o => o with { Truncated = true } });
        var verdict = await Run(model, fixture);
        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
    }

    [Fact]
    public async Task Blocks_act_and_judges_inconclusive_without_a_tree()
    {
        var (model, _) = ScriptedDecision((_, _, _) => new ScriptedAnswer("holds"));
        var act = Context(
            new ContextOptions { Tree = Buttons, Observation = o => o with { TreeUnavailable = true, Tree = null } }
        );
        var blocked = await Run(model, act);
        Assert.Equal(StepVerdictStatus.Blocked, blocked.Status);
        Assert.Equal("AUTOMATION_UNSUPPORTED", blocked.ErrorCode);
        var assert = Context(
            new ContextOptions
            {
                Kind = ExecutorStepKind.Assert,
                Tree = Buttons,
                Observation = o => o with { TreeUnavailable = true, Tree = null },
            }
        );
        var inconclusive = await Run(model, assert);
        Assert.Equal(StepVerdictStatus.Failed, inconclusive.Status);
        Assert.Equal("ASSERTION_INCONCLUSIVE", inconclusive.ErrorCode);
    }

    [Fact]
    public async Task Judges_a_truncated_assertion_on_what_is_there()
    {
        var (model, _) = ScriptedDecision((_, _, _) => new ScriptedAnswer("holds"));
        var fixture = Context(
            new ContextOptions
            {
                Kind = ExecutorStepKind.Assert,
                Tree = Buttons,
                Observation = o => o with { Truncated = true },
            }
        );
        var verdict = await Run(model, fixture);
        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
    }
}
