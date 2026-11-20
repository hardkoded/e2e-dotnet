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
/// covers "engine failure", "times out", and "soft failure then skipped" by their verdict only. Not ported: "takes a mark from a beforeEach
/// hook and refuses one from an afterEach hook" (no <c>test.fail</c> call, so no hook to mark from or refuse), and "does not retry an expected
/// failure, retries an unexpected pass, and a failure before the mark retries as usual" (the port has no retry policy of its own).
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
}
