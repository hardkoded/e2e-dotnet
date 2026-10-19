// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.Traces;
using static E2E.Tests.ReportFailureText.FailureTextKit;

namespace E2E.Tests.ReportFailureText;

/// <summary>Upstream's <c>describe('failedStepOf')</c> (<c>tests/unit/report-failure-text.test.ts</c>).</summary>
public sealed class FailedStepOfTests
{
    private static readonly StepRecord Caught = Step(1, api: "locator.waitFor", status: StepStatus.Failed, error: ReportBuilders.Error("LOCATOR_NOT_FOUND"));

    private static readonly StepRecord Ended = Step(3, api: "expect.toHaveCount", status: StepStatus.Failed, error: ReportBuilders.Error("ASSERTION_FAILED"));

    private static readonly StepRecord[] Steps = [Step(0), Caught, Step(2), Ended];

    private static ReportAttempt Attempt(IReadOnlyList<StepRecord> steps, ReportError? error) => new() { Steps = steps, Error = error };

    private static SourceLocation At(int line) => new("tests/a.e2e.ts", line, 3);

    [Fact]
    public void Names_the_step_whose_error_ended_the_attempt_not_an_earlier_failure_the_test_caught_and_went_on_from()
    {
        Assert.Equal(new FailedStep(3, Ended), FailureText.FailedStepOf(Attempt(Steps, ReportBuilders.Error("ASSERTION_FAILED", phase: "body"))));

        // Same code twice: the body ends at the throw, so the last one is it.
        var alike = Step(3, api: "locator.waitFor", status: StepStatus.Failed, error: ReportBuilders.Error("LOCATOR_NOT_FOUND"));
        Assert.Equal(new FailedStep(3, alike), FailureText.FailedStepOf(Attempt([Step(0), Caught, Step(2), alike], ReportBuilders.Error("LOCATOR_NOT_FOUND", phase: "body"))));
    }

    [Fact]
    public void Takes_the_step_whose_error_unwound_through_the_line_the_attempt_error_names_and_none_when_no_step_error_did()
    {
        // A step's source is its call site, one line below the `await` a split call's error unwinds through.
        var first = Step(1, api: "expect.toBeHidden", status: StepStatus.Failed, source: At(6), error: ReportBuilders.Error(source: At(5)));
        var second = Step(2, api: "expect.toHaveText", status: StepStatus.Failed, source: At(8), error: ReportBuilders.Error(source: At(7)));

        // Soft assertions: the one error unwinds through the first failure's stack, so the first failed step is it.
        Assert.Equal(new FailedStep(1, first), FailureText.FailedStepOf(Attempt([Step(0), first, second], ReportBuilders.Error(source: At(5)))));
        Assert.Equal(new FailedStep(2, second), FailureText.FailedStepOf(Attempt([Step(0), first, second], ReportBuilders.Error(source: At(7)))));

        // A value matcher throws from a line no step's error names; the caught step before it does not stand in.
        Assert.Null(FailureText.FailedStepOf(Attempt([Step(0), first], ReportBuilders.Error(source: At(9)))));
    }

    [Fact]
    public void Keeps_to_the_phase_the_error_names_so_a_hook_that_failed_alike_after_the_body_does_not_stand_in_for_it()
    {
        var inHook = Step(4, phase: StepPhase.AfterEach, status: StepStatus.Failed, error: ReportBuilders.Error("ASSERTION_FAILED"));

        Assert.Equal(new FailedStep(3, Ended), FailureText.FailedStepOf(Attempt([.. Steps, inHook], ReportBuilders.Error("ASSERTION_FAILED", phase: "body"))));
        Assert.Equal(new FailedStep(4, inHook), FailureText.FailedStepOf(Attempt([.. Steps, inHook], ReportBuilders.Error("ASSERTION_FAILED", phase: "afterEach"))));
    }

    [Fact]
    public void Falls_back_to_the_step_the_attempt_cut_when_no_step_carries_the_code_and_names_none_for_an_error_from_no_step_or_for_no_error()
    {
        var cut = Step(3, status: StepStatus.Cancelled, error: ReportBuilders.Error("CANCELLED"));

        Assert.Equal(new FailedStep(3, cut), FailureText.FailedStepOf(Attempt([Step(0), Caught, Step(2), cut], ReportBuilders.Error("TEST_TIMEOUT", phase: "body"))));
        Assert.Null(FailureText.FailedStepOf(Attempt(Steps, ReportBuilders.Error("UNEXPECTED_ERROR", phase: "body"))));
        Assert.Null(FailureText.FailedStepOf(Attempt(Steps, null)));
    }
}
