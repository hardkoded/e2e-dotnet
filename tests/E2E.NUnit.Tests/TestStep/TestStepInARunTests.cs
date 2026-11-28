// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework.Interfaces;
using static E2E.NUnit.Tests.TestStep.TestStepKit;

namespace E2E.NUnit.Tests.TestStep;

/// <summary>
/// <c>describe('test.step in a run')</c> of upstream's <c>tests/integration/test-step.test.ts</c> (tester-army/e2e#2312), whose tests run the CLI over a
/// project. These run the fixtures through NUnit and read each run's <c>report.json</c>. The port's clock starts before the engine launches, so the timeout
/// of <c>hangs in a step</c> is 10 s, not 1500 ms. The suite's <c>test.beforeAll</c> that seeds with a <c>test.step</c> is not ported: the port has no suite
/// hooks that <c>E2E</c> times, NUnit owns <c>[OneTimeSetUp]</c>. The skip is <c>Fixme</c>, which the step passes through; the port does not tell
/// a host's own <c>Assert.Ignore</c> from a failure in a step.
/// </summary>
public sealed class TestStepInARunTests
{
    [OneTimeSetUp]
    public void ClearResults() => TestStepResults.Clear();

    [OneTimeTearDown]
    public void RemoveResults() => TestStepResults.Clear();

    private static ITestResult RunOf(string method) => NestedTestStepRun.Run(method).Single();

    [Test]
    public void Records_each_step_with_the_calls_inside_it_as_its_children_in_a_report_the_schema_takes()
    {
        var run = RunOf(nameof(StepInARunFixture.Reads_the_heading));
        Assert.That(run.ResultState.Status, Is.EqualTo(TestStatus.Passed), run.Message);
        var report = Report(typeof(StepInARunFixture));
        E2E.Tests.ReportSchema.AssertValidReport(report);
        Assert.That(
            Told(ResultByTitle(report, nameof(StepInARunFixture.Reads_the_heading))),
            Is.EqualTo(
                new[]
                {
                    (0, "step", "test.step", "Open the app", (int?)null, (string?)"beforeEach", "passed"),
                    (1, "app", "app.open", "...", (int?)0, (string?)"beforeEach", "passed"),
                    (2, "step", "test.step", "Read the heading", (int?)null, (string?)null, "passed"),
                    (3, "step", "test.step", "Find it", (int?)2, (string?)null, "passed"),
                    (4, "locator", "locator.waitFor", "...", (int?)3, (string?)null, "passed"),
                }
            )
        );
    }

    [Test]
    public void Passes_a_step_the_test_skipped_itself_from()
    {
        var run = RunOf(nameof(StepInARunFixture.Skips_from_inside_a_step));
        Assert.That(run.ResultState.Status, Is.EqualTo(TestStatus.Skipped), run.Message);
        var result = ResultByTitle(
            Report(typeof(StepInARunFixture)),
            nameof(StepInARunFixture.Skips_from_inside_a_step)
        );
        Assert.That((string?)result["status"], Is.EqualTo("skipped"));
        var group = StepsOf(result).Single(step => (string?)step!["label"] == "Check the platform")!;
        Assert.That((string?)group["kind"], Is.EqualTo("step"));
        Assert.That((string?)group["status"], Is.EqualTo("passed"));
        Assert.That(group["error"], Is.Null);
    }

    [Test]
    public void Ends_a_step_the_test_timed_out_in_as_not_passed()
    {
        var run = RunOf(nameof(StepInARunFixture.Hangs_in_a_step));
        Assert.That(run.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        var report = Report(typeof(StepInARunFixture));
        var result = ResultByTitle(report, nameof(StepInARunFixture.Hangs_in_a_step));
        Assert.That((string?)result["status"], Is.EqualTo("timed-out"));
        var group = StepsOf(result).Single(step => (string?)step!["label"] == "Wait for nothing")!;
        Assert.That((string?)group["status"], Is.Not.EqualTo("passed"));
        E2E.Tests.ReportSchema.AssertValidReport(report);
    }

    [Test]
    public void Fails_the_step_with_the_call_that_failed_in_it_and_leaves_the_error_to_that_call()
    {
        var run = RunOf(nameof(StepInARunFixture.Taps_a_missing_button));
        Assert.That(run.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        var result = ResultByTitle(Report(typeof(StepInARunFixture)), nameof(StepInARunFixture.Taps_a_missing_button));
        Assert.That((string?)result["status"], Is.EqualTo("failed"));
        var attempt = result["attempts"]![0]!;
        Assert.That((string?)attempt["error"]!["code"], Is.EqualTo("LOCATOR_NOT_FOUND"));
        var group = StepsOf(result).Single(step => (string?)step!["label"] == "Log in as test user")!;
        var tap = StepsOf(result).Single(step => (string?)step!["api"] == "locator.tap")!;
        Assert.That((string?)group["kind"], Is.EqualTo("step"));
        Assert.That((string?)group["status"], Is.EqualTo("failed"));
        Assert.That(group["error"], Is.Null);
        Assert.That((int?)tap["parent"], Is.EqualTo((int)group["index"]!));
        Assert.That((string?)tap["status"], Is.EqualTo("failed"));
        Assert.That((string?)tap["error"]!["code"], Is.EqualTo("LOCATOR_NOT_FOUND"));
    }
}
