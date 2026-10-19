// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.Traces;
using static E2E.Tests.ReportFailureText.FailureTextKit;

namespace E2E.Tests.ReportFailureText;

/// <summary>Upstream's <c>describe('attemptsLine')</c> (<c>tests/unit/report-failure-text.test.ts</c>).</summary>
public sealed class AttemptsLineTests
{
    [Fact]
    public void Folds_consecutive_attempts_that_failed_alike_into_one_run_and_names_the_code_only_when_it_changes()
    {
        var early = Failed(steps: [Step(0, status: StepStatus.Failed, error: ReportBuilders.Error())]);
        var drifted = Result(attempts: [early, Failed(), Failed(), Failed()]);
        Assert.Equal("Failed on all 4 attempts: **ASSERTION_FAILED** at step 1, then at step 2 (3 times).", FailureText.AttemptsLine(drifted, Outcome.Of(drifted)));

        var mixed = Result(attempts: [Failed(ReportBuilders.Error("LOCATOR_NOT_FOUND")), Failed(ReportBuilders.Error("LOCATOR_NOT_FOUND")), Failed()]);
        Assert.Equal("Failed on all 3 attempts: **LOCATOR_NOT_FOUND** at step 2 (2 times), then **ASSERTION_FAILED** at step 2.", FailureText.AttemptsLine(mixed, Outcome.Of(mixed)));
    }

    [Fact]
    public void Tells_a_flaky_test_its_failures_agreed_and_ignores_an_attempt_that_never_reached_the_test()
    {
        var flaky = Result(ResultStatus.Flaky, [Failed(), Failed(), ReportBuilders.Attempt(AttemptStatus.Passed)]);
        Assert.Equal("Failed the same way on 2 attempts: **ASSERTION_FAILED** at step 2. The retry that passed is the exception.", FailureText.AttemptsLine(flaky, Outcome.Of(flaky)));

        var launchFailed = Result(attempts: [ReportBuilders.Attempt(AttemptStatus.Failed), Failed()]);
        Assert.Null(FailureText.AttemptsLine(launchFailed, Outcome.Of(launchFailed)));
    }
}
