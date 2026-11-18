// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.Executor.ExecutorContextHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.Executor;

/// <summary>Upstream's <c>describe('assert')</c> of <c>executor.test.ts</c>.</summary>
public sealed class AssertTests
{
    [Fact]
    public async Task Passes_holds_fails_fails_and_calls_the_rest_inconclusive()
    {
        foreach (
            var (answer, status, code) in new (string, StepVerdictStatus, string?)[]
            {
                ("holds", StepVerdictStatus.Passed, null),
                ("fails", StepVerdictStatus.Failed, "ASSERTION_FAILED"),
                ("inconclusive", StepVerdictStatus.Failed, "ASSERTION_INCONCLUSIVE"),
            }
        )
        {
            var (model, requests) = ScriptedDecision((_, _, _) => new ScriptedAnswer(answer));
            var fixture = Context(new ContextOptions { Kind = ExecutorStepKind.Assert, Tree = Buttons });
            var verdict = await Run(model, fixture);
            Assert.Equal(status, verdict.Status);
            if (code is not null)
            {
                Assert.Equal(code, verdict.ErrorCode);
            }

            Assert.Equal(["verdict"], requests[0].Questions.Keys);
        }
    }

    [Fact]
    public async Task Treats_a_gated_out_verdict_as_inconclusive()
    {
        var (model, _) = ScriptedDecision(
            (_, _, _) =>
                new ScriptedAnswer(
                    "holds",
                    new Dictionary<string, double>
                    {
                        ["holds"] = 0.4,
                        ["fails"] = 0.3,
                        ["inconclusive"] = 0.3,
                    },
                    0.9
                )
        );
        var fixture = Context(new ContextOptions { Kind = ExecutorStepKind.Assert, Tree = Buttons });
        var verdict = await Run(model, fixture, minProbability: 0.5);
        Assert.Equal(StepVerdictStatus.Failed, verdict.Status);
        Assert.Equal("ASSERTION_INCONCLUSIVE", verdict.ErrorCode);
    }
}
