// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.Traces;
using static E2E.Tests.ReportFailureText.FailureTextKit;
using FailureText = E2E.Reports.FailureText;
using Outcome = E2E.Reports.Outcome;

namespace E2E.Tests.ReportFailureText;

/// <summary>Upstream's <c>describe('failureSource')</c> (<c>tests/unit/report-failure-text.test.ts</c>).</summary>
public sealed class FailureSourceTests
{
    private static E2E.Reports.ReportSource Told(ReportResult result)
    {
        var told = ReportBuilders.Public(result);
        return FailureText.FailureSource(told, FailureText.ToldAttempt(told, Outcome.Of(told)));
    }

    private static E2E.Reports.ReportSource Source(string file, int line, int column) => new() { File = file, Line = line, Column = column };

    [Fact]
    public void Prefers_the_line_the_error_unwound_through_then_the_failing_steps_own_call_then_the_tests_declaration()
    {
        var fromError = Result(attempts: [Failed(ReportBuilders.Error(source: new("tests/a.e2e.ts", 14, 3)))]);
        Assert.Equal(Source("tests/a.e2e.ts", 14, 3), Told(fromError));

        var fromStep = Result(attempts: [Failed(steps: [Step(0), Step(1, status: StepStatus.Failed, error: ReportBuilders.Error(), source: new("tests/a.e2e.ts", 9, 5))])]);
        Assert.Equal(Source("tests/a.e2e.ts", 9, 5), Told(fromStep));

        var declared = Result();
        Assert.Equal(ReportBuilders.Public(declared).Source, Told(declared));
    }

    [Fact]
    public void Names_the_line_of_the_step_whose_error_ended_the_attempt_not_a_caught_failures_before_it()
    {
        var caughtFirst = Result(attempts:
        [
            Failed(steps:
            [
                Step(0, status: StepStatus.Failed, error: ReportBuilders.Error("LOCATOR_NOT_FOUND"), source: new("tests/a.e2e.ts", 8, 5)),
                Step(1, status: StepStatus.Failed, error: ReportBuilders.Error(), source: new("tests/a.e2e.ts", 15, 5)),
            ]),
        ]);

        Assert.Equal(Source("tests/a.e2e.ts", 15, 5), Told(caughtFirst));
    }
}
