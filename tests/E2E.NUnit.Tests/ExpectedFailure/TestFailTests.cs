// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.ExpectedFailure;

/// <summary>
/// <c>describe('test.fail')</c> of upstream's <c>tests/integration/expected-failure.test.ts</c>, whose tests run the CLI over a project. These run
/// <see cref="ExpectedFailureFixture"/> through NUnit, with <c>[Property("E2E.Fail", reason)]</c> for the <c>fail</c> option and a property set in the
/// body for <c>test.fail(...)</c>. The first <c>it</c> is <c>Partial_</c>: it checks the verdict <c>dotnet test</c> reports and not the report
/// (<c>expectedFailure</c> on the result and the attempt, the error's code and category, the summary, the exit code, or the JUnit text), and
/// covers "engine failure", "times out", and "soft failure then skipped" by their verdict only. The hook, retry, and rejection tests read the
/// NUnit verdict and the attempt counts, not the report (<c>attempts</c>, <c>status: 'flaky'</c>, the exit code, <c>run.errors</c>, <c>serialGroups</c>),
/// so each is <c>Partial_</c> too. The retry test also leaves out "retries an unexpected pass" (NUnit decides a <c>[Retry]</c> from the body's result, before the teardown settles the mark, so an unexpected pass runs once) and "passes once, then fails as expected" (it needs that retry). <c>[Retry(2)]</c> stands for upstream's <c>retries: 2</c>: NUnit owns the retry policy.
/// </summary>
public sealed class TestFailTests
{
    [Test]
    public void Partial_passes_a_marked_test_that_fails_fails_one_that_passes_and_keeps_a_skip()
    {
        Assert.That(
            NestedRun.Run(nameof(ExpectedFailureFixture.Registered_fails)).ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );
        Assert.That(
            NestedRun.Run(nameof(ExpectedFailureFixture.Option_fails)).ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );
        Assert.That(
            NestedRun.Run(nameof(ExpectedFailureFixture.Runtime_fails)).ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );
        Assert.That(
            NestedRun.Run(nameof(ExpectedFailureFixture.Condition_false_runs_as_normal)).ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );

        var unexpected = NestedRun.Run(nameof(ExpectedFailureFixture.Option_passes));
        Assert.That(unexpected.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(unexpected.Message, Does.Contain("UNEXPECTED_PASS").And.Contain("\"gap is fixed\""));

        Assert.That(
            NestedRun.Run(nameof(ExpectedFailureFixture.Marked_then_skipped)).ResultState.Status,
            Is.EqualTo(TestStatus.Skipped)
        );

        // A skip wins beside a failure too: the attempt fails as an unmarked one would.
        Assert.That(
            NestedRun.Run(nameof(ExpectedFailureFixture.Soft_failure_then_skipped)).ResultState.Status,
            Is.EqualTo(TestStatus.Failed)
        );

        // An engine failure is not a test failure: the mark does not cover it.
        Assert.That(
            NestedRun.Run(nameof(ExpectedFailureFixture.Engine_failure)).ResultState.Status,
            Is.EqualTo(TestStatus.Failed)
        );

        // A timeout is a product failure the mark covers.
        Assert.That(
            NestedRun.Run(nameof(ExpectedFailureTimeoutFixture.Times_out)).ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );
    }

    [Test]
    public void Partial_takes_a_mark_from_a_beforeEach_hook_and_refuses_one_from_an_afterEach_hook()
    {
        Assert.That(
            NestedRun.Run(nameof(MarkedEarlyFixture.Fails_after_a_beforeEach_mark)).ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );

        var late = NestedRun.Run(nameof(MarkedLateFixture.Fails_before_an_afterEach_mark));
        Assert.That(late.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(late.Message, Does.Contain("cannot mark a test from an afterEach hook"));
    }

    [Test]
    public void Partial_does_not_retry_an_expected_failure_retries_an_unexpected_pass_and_a_failure_before_the_mark_retries_as_usual()
    {
        Assert.That(
            NestedRun.Run(nameof(ExpectedFailureRetriesFixture.Expected_failure)).ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );
        Assert.That(ExpectedFailureRetriesFixture.ExpectedFailureRuns, Is.EqualTo(1));

        var unexpected = NestedRun.Run(nameof(ExpectedFailureRetriesFixture.Unexpected_pass));
        Assert.That(unexpected.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(unexpected.Message, Does.Contain("UNEXPECTED_PASS"));

        var late = NestedRun.Run(nameof(ExpectedFailureRetriesFixture.Fails_before_the_mark));
        Assert.That(late.ResultState.Status, Is.EqualTo(TestStatus.Passed), late.Message);
        Assert.That(ExpectedFailureRetriesFixture.FailRuns, Is.EqualTo(2));
    }

    [Test]
    public void Partial_rejects_test_fail_outside_a_body_in_a_setup_test_and_in_a_serial_group()
    {
        var outside = Assert.Throws<InvalidOperationException>(() =>
            new ExpectedFailureSerialFixture().Fail(true, "not here")
        );
        Assert.That(outside?.Message, Does.Contain("must be called inside a test body"));

        var setup = NestedRun.Run("sign in");
        Assert.That(setup.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(setup.Message, Does.Contain("INVALID_ARGUMENT").And.Contain("setup test"));

        var member = NestedRun.Run(nameof(ExpectedFailureSerialFixture.Step_1));
        Assert.That(member.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(member.Message, Does.Contain("INVALID_ARGUMENT").And.Contain("serial group member"));
    }
}
