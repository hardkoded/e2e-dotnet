// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using E2E.Internal;

namespace E2E.Tests.ExpectedFailure;

/// <summary>Upstream's <c>describe('settleExpectedFailure')</c> (<c>tests/unit/expected-failure.test.ts</c>).</summary>
public sealed class SettleExpectedFailureTests
{
    private static ReportError Failure(string category, string code = "ASSERTION_FAILED", string? phase = null) =>
        new(code, "expected 1, got 0", phase) { Category = category };

    private static MarkableAttempt Settle(MarkableAttempt attempt, string? reason, Func<string, string>? redact = null)
    {
        redact ??= text => text;
        E2E.ExpectedFailure.SettleExpectedFailure(
            attempt,
            reason,
            new ExpectedFailureSerializer(
                error => ReportError.Of(ErrorSerializer.Serialize(error, redact: redact)),
                redact
            )
        );
        return attempt;
    }

    private static MarkableAttempt Attempt(
        AttemptStatus status,
        ReportError? error = null,
        ReportSkip? skip = null,
        params ReportError[] secondary
    ) =>
        new()
        {
            Status = status,
            Error = error,
            Skip = skip,
            SecondaryErrors = secondary,
        };

    [Fact]
    public void Leaves_an_unmarked_attempt_alone()
    {
        var error = Failure("test");
        var settled = Settle(Attempt(AttemptStatus.Failed, error), null);
        Assert.Equal(AttemptStatus.Failed, settled.Status);
        Assert.Same(error, settled.Error);
        Assert.Null(settled.ExpectedFailure);
    }

    [Fact]
    public void Passes_a_test_failure_or_timeout_keeping_the_error_beside_the_reason()
    {
        var error = Failure("test");
        var settled = Settle(Attempt(AttemptStatus.Failed, error), "known gap");
        Assert.Equal(AttemptStatus.Passed, settled.Status);
        Assert.Null(settled.Error);
        Assert.Equal(new ExpectedFailureInfo("known gap", error), settled.ExpectedFailure);

        var timeout = Failure("test", "TEST_TIMEOUT");
        var timedOut = Settle(Attempt(AttemptStatus.TimedOut, timeout), "slow");
        Assert.Equal(AttemptStatus.Passed, timedOut.Status);
        Assert.Null(timedOut.Error);
        Assert.Equal(new ExpectedFailureInfo("slow", timeout), timedOut.ExpectedFailure);
    }

    [Fact]
    public void Fails_a_pass_with_UNEXPECTED_PASS_naming_the_reason()
    {
        var settled = Settle(Attempt(AttemptStatus.Passed), "known gap");
        Assert.Equal(AttemptStatus.Failed, settled.Status);
        Assert.Equal("test", settled.Error?.Category);
        Assert.Equal("UNEXPECTED_PASS", settled.Error?.Code);
        Assert.False(settled.Error?.Retryable);
        Assert.Contains("\"known gap\"", settled.Error?.Message);
        Assert.Equal(new ExpectedFailureInfo("known gap"), settled.ExpectedFailure);
    }

    [Fact]
    public void Keeps_infrastructure_failures_and_interrupts_as_they_are_recording_the_mark()
    {
        var engine = Failure("infrastructure", "ENGINE_FAILURE");
        var failed = Settle(Attempt(AttemptStatus.Failed, engine), "gap");
        Assert.Equal(AttemptStatus.Failed, failed.Status);
        Assert.Same(engine, failed.Error);
        Assert.Equal(new ExpectedFailureInfo("gap"), failed.ExpectedFailure);

        var interrupt = Failure("interrupted", "INTERRUPTED");
        var interrupted = Settle(Attempt(AttemptStatus.Interrupted, interrupt), "gap");
        Assert.Equal(AttemptStatus.Interrupted, interrupted.Status);
        Assert.Same(interrupt, interrupted.Error);
        Assert.Equal(new ExpectedFailureInfo("gap"), interrupted.ExpectedFailure);
    }

    [Fact]
    public void Does_not_cover_a_blocked_agent_verdict()
    {
        var blocked = Failure("test", "STEP_BUDGET_EXHAUSTED");
        var settled = Settle(Attempt(AttemptStatus.Failed, blocked), "gap");
        Assert.Equal(AttemptStatus.Failed, settled.Status);
        Assert.Same(blocked, settled.Error);
        Assert.Equal(new ExpectedFailureInfo("gap"), settled.ExpectedFailure);
    }

    [Fact]
    public void Does_not_cover_a_test_failure_with_an_infrastructure_error_beside_it_outside_cleanup()
    {
        var crash = Failure("infrastructure", "ENGINE_FAILURE", "afterEach");
        Assert.Equal(
            AttemptStatus.Failed,
            Settle(Attempt(AttemptStatus.Failed, Failure("test"), null, crash), "gap").Status
        );
        var cleanup = crash with { Phase = "cleanup" };
        Assert.Equal(
            AttemptStatus.Passed,
            Settle(Attempt(AttemptStatus.Failed, Failure("test"), null, cleanup), "gap").Status
        );
    }

    [Fact]
    public void Does_not_cover_a_test_that_misuses_the_API_or_a_failed_suite_hook()
    {
        foreach (
            var code in new[] { "INVALID_ARGUMENT", "INVALID_LOCATOR", "UNSUPPORTED_CAPABILITY", "STEP_NOT_AWAITED" }
        )
        {
            Assert.Equal(
                AttemptStatus.Failed,
                Settle(Attempt(AttemptStatus.Failed, Failure("test", code)), "gap").Status
            );
        }

        var hook = Failure("test", "HOOK_FAILED", "beforeAll");
        var settled = Settle(Attempt(AttemptStatus.Failed, hook), "gap");
        Assert.Equal(AttemptStatus.Failed, settled.Status);
        Assert.Same(hook, settled.Error);
        Assert.Equal(new ExpectedFailureInfo("gap"), settled.ExpectedFailure);

        var misuse = Failure("test", "STEP_NOT_AWAITED", "body");
        Assert.Equal(
            AttemptStatus.Failed,
            Settle(Attempt(AttemptStatus.Failed, Failure("test"), null, misuse), "gap").Status
        );
    }

    [Fact]
    public void Redacts_and_bounds_the_reason()
    {
        string Redact(string text) => text.Replace("hunter2", "[REDACTED]");
        var covered = Settle(Attempt(AttemptStatus.Failed, Failure("test")), "token hunter2 leaks", Redact);
        Assert.Equal("token [REDACTED] leaks", covered.ExpectedFailure?.Reason);
        var unexpected = Settle(Attempt(AttemptStatus.Passed), "hunter2", Redact);
        Assert.DoesNotContain("hunter2", unexpected.Error?.Message);
        var bounded = Settle(Attempt(AttemptStatus.Passed), new string('x', 9000));
        Assert.Equal(8192, Encoding.UTF8.GetByteCount(bounded.ExpectedFailure!.Reason));
    }

    [Fact]
    public void Keeps_a_CRLF_line_break_in_the_reason_and_replaces_a_lone_carriage_return()
    {
        var settled = Settle(Attempt(AttemptStatus.Failed, Failure("test")), "known\r\ngap\rhere");
        Assert.Equal("known\r\ngap�here", settled.ExpectedFailure?.Reason);
    }

    [Fact]
    public void Lets_a_skip_win_also_beside_a_failure()
    {
        var skipped = Settle(Attempt(AttemptStatus.Skipped), "gap");
        Assert.Equal(AttemptStatus.Skipped, skipped.Status);
        Assert.Null(skipped.ExpectedFailure);

        var skip = new ReportSkip("explicit", "n/a");
        var error = Failure("test");
        var failed = Settle(Attempt(AttemptStatus.Failed, error, skip), "gap");
        Assert.Equal(AttemptStatus.Failed, failed.Status);
        Assert.Same(error, failed.Error);
        Assert.Same(skip, failed.Skip);
        Assert.Null(failed.ExpectedFailure);
    }
}
