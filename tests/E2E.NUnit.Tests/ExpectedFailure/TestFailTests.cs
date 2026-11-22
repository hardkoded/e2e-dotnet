// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.ExpectedFailure;

/// <summary>
/// <c>describe('test.fail')</c> of upstream's <c>tests/integration/expected-failure.test.ts</c>, whose tests run the CLI over a project. These run
/// the fixtures through NUnit, with <c>[Property("E2E.Fail", reason)]</c> for the <c>fail</c> option and a property set in the body for
/// <c>test.fail(...)</c>, then read the <c>report.json</c> the run wrote (the schema-checked <c>run.results</c>, <c>summary</c>, and <c>exitCode</c>).
/// Each test is <c>Partial_</c> for what the port cannot check yet: the first leaves out the JUnit text (the port has no JUnit report); the retry test leaves out
/// "retries an unexpected pass" (NUnit decides a <c>[Retry]</c> from the body's result, before the teardown settles the mark, so an unexpected pass runs once)
/// and "passes once, then fails as expected" (it needs that retry); the rejection test leaves out the <c>COLLECTION_ERROR</c> run error (the port throws
/// <see cref="InvalidOperationException"/> where upstream collects) and <c>serialGroups[0].attempts[0].members[0].error</c> (the report has no serial groups).
/// <c>[Retry(2)]</c> stands for upstream's <c>retries: 2</c>: NUnit owns the retry policy.
/// </summary>
public sealed class TestFailTests
{
    [OneTimeSetUp]
    public void ClearResults() => ExpectedFailureResults.Clear();

    [OneTimeTearDown]
    public void RemoveResults() => ExpectedFailureResults.Clear();

    [Test]
    public void Partial_passes_a_marked_test_that_fails_fails_one_that_passes_and_keeps_the_reason_and_error_in_the_report()
    {
        string[] tests =
        [
            nameof(ExpectedFailureFixture.Registered_fails),
            nameof(ExpectedFailureFixture.Option_fails),
            nameof(ExpectedFailureFixture.Runtime_fails),
            nameof(ExpectedFailureFixture.Condition_false_runs_as_normal),
            nameof(ExpectedFailureFixture.Option_passes),
            nameof(ExpectedFailureFixture.Marked_then_skipped),
            nameof(ExpectedFailureFixture.Soft_failure_then_skipped),
            nameof(ExpectedFailureFixture.Engine_failure),
        ];
        var verdicts = tests.ToDictionary(test => test, test => NestedRun.Run(test));
        var timedOut = NestedRun.Run(nameof(ExpectedFailureTimeoutFixture.Times_out));

        Assert.That(
            verdicts[nameof(ExpectedFailureFixture.Registered_fails)].ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );
        Assert.That(
            verdicts[nameof(ExpectedFailureFixture.Option_fails)].ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );
        Assert.That(
            verdicts[nameof(ExpectedFailureFixture.Runtime_fails)].ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );
        Assert.That(timedOut.ResultState.Status, Is.EqualTo(TestStatus.Passed));
        Assert.That(
            verdicts[nameof(ExpectedFailureFixture.Option_passes)].ResultState.Status,
            Is.EqualTo(TestStatus.Failed)
        );
        Assert.That(
            verdicts[nameof(ExpectedFailureFixture.Marked_then_skipped)].ResultState.Status,
            Is.EqualTo(TestStatus.Skipped)
        );
        Assert.That(
            verdicts[nameof(ExpectedFailureFixture.Engine_failure)].ResultState.Status,
            Is.EqualTo(TestStatus.Failed)
        );

        var report = ExpectedFailureReport.Read(typeof(ExpectedFailureFixture));
        var run = report["run"]!;

        var registered = ExpectedFailureReport.ByTitle(report, nameof(ExpectedFailureFixture.Registered_fails));
        Assert.That((string?)registered["status"], Is.EqualTo("passed"));
        Assert.That((string?)registered["expectedFailure"]!["reason"], Is.EqualTo("expected to fail"));
        Assert.That(registered["expectedFailure"]!["error"], Is.Null);
        Assert.That(registered["attempts"]![0]!["error"], Is.Null);
        var registeredMark = registered["attempts"]![0]!["expectedFailure"]!;
        Assert.That((string?)registeredMark["reason"], Is.EqualTo("expected to fail"));
        Assert.That((string?)registeredMark["error"]!["category"], Is.EqualTo("test"));
        Assert.That((string?)registeredMark["error"]!["code"], Is.EqualTo("ASSERTION_FAILED"));

        var option = ExpectedFailureReport.ByTitle(report, nameof(ExpectedFailureFixture.Option_fails));
        Assert.That((string?)option["status"], Is.EqualTo("passed"));
        Assert.That((string?)option["expectedFailure"]!["reason"], Is.EqualTo("known gap"));
        var optionError = option["attempts"]![0]!["expectedFailure"]!["error"]!;
        Assert.That((string?)optionError["code"], Is.EqualTo("ERROR"));
        Assert.That((string?)optionError["message"], Does.Contain("the gap"));

        var runtime = ExpectedFailureReport.ByTitle(report, nameof(ExpectedFailureFixture.Runtime_fails));
        Assert.That((string?)runtime["status"], Is.EqualTo("passed"));
        Assert.That((string?)runtime["expectedFailure"]!["reason"], Is.EqualTo("gap at runtime"));

        var slow = ExpectedFailureReport.ByTitle(report, nameof(ExpectedFailureTimeoutFixture.Times_out));
        Assert.That((string?)slow["status"], Is.EqualTo("passed"));
        Assert.That((string?)slow["expectedFailure"]!["reason"], Is.EqualTo("too slow"));
        Assert.That((string?)slow["attempts"]![0]!["expectedFailure"]!["error"]!["code"], Is.EqualTo("TEST_TIMEOUT"));

        var normal = ExpectedFailureReport.ByTitle(
            report,
            nameof(ExpectedFailureFixture.Condition_false_runs_as_normal)
        );
        Assert.That((string?)normal["status"], Is.EqualTo("passed"));
        Assert.That(normal["expectedFailure"], Is.Null);
        Assert.That(normal["attempts"]![0]!["expectedFailure"], Is.Null);

        var unexpected = ExpectedFailureReport.ByTitle(report, nameof(ExpectedFailureFixture.Option_passes));
        Assert.That((string?)unexpected["status"], Is.EqualTo("failed"));
        Assert.That((string?)unexpected["expectedFailure"]!["reason"], Is.EqualTo("gap is fixed"));
        var unexpectedError = unexpected["attempts"]![0]!["error"]!;
        Assert.That((string?)unexpectedError["category"], Is.EqualTo("test"));
        Assert.That((string?)unexpectedError["code"], Is.EqualTo("UNEXPECTED_PASS"));
        Assert.That((string?)unexpectedError["message"], Does.Contain("\"gap is fixed\""));

        var skipped = ExpectedFailureReport.ByTitle(report, nameof(ExpectedFailureFixture.Marked_then_skipped));
        Assert.That((string?)skipped["status"], Is.EqualTo("skipped"));
        Assert.That(skipped["expectedFailure"], Is.Null);

        // A skip wins beside a failure too: the attempt fails as an unmarked one would.
        var softSkipped = ExpectedFailureReport.ByTitle(
            report,
            nameof(ExpectedFailureFixture.Soft_failure_then_skipped)
        );
        Assert.That((string?)softSkipped["status"], Is.EqualTo("failed"));
        Assert.That(softSkipped["attempts"]![0]!["expectedFailure"], Is.Null);

        // An engine failure is not a test failure: the mark records but does not cover it.
        var engine = ExpectedFailureReport.ByTitle(report, nameof(ExpectedFailureFixture.Engine_failure));
        Assert.That((string?)engine["status"], Is.EqualTo("failed"));
        Assert.That((string?)engine["attempts"]![0]!["error"]!["category"], Is.EqualTo("infrastructure"));
        Assert.That((string?)engine["attempts"]![0]!["error"]!["code"], Is.EqualTo("ENGINE_FAILURE"));
        Assert.That((string?)engine["attempts"]![0]!["expectedFailure"]!["reason"], Is.EqualTo("engine gap"));
        Assert.That(engine["attempts"]![0]!["expectedFailure"]!["error"], Is.Null);

        Assert.That((int)run["summary"]!["passed"]!, Is.EqualTo(5));
        Assert.That((int)run["summary"]!["failed"]!, Is.EqualTo(3));
        Assert.That((int)run["summary"]!["skipped"]!, Is.EqualTo(1));
        Assert.That((int)run["exitCode"]!, Is.EqualTo(3));
    }

    [Test]
    public void Marks_every_test_in_a_describe_fail_group_each_judged_on_its_own()
    {
        Assert.That(
            NestedRun.Run(nameof(DescribeFailFixture.Still_broken)).ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );
        Assert.That(
            NestedRun.Run(nameof(DescribeFailFixture.Fixed_since)).ResultState.Status,
            Is.EqualTo(TestStatus.Failed)
        );
        Assert.That(
            NestedRun.Run(nameof(KnownGapsFixture.Card_form)).ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );
        Assert.That(
            NestedRun.Run(nameof(OutsideTheGroupFixture.Outside_the_group)).ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );

        var report = ExpectedFailureReport.Read(typeof(DescribeFailFixture));
        var broken = ExpectedFailureReport.ByTitle(report, nameof(DescribeFailFixture.Still_broken));
        Assert.That((string?)broken["status"], Is.EqualTo("passed"));
        Assert.That((string?)broken["expectedFailure"]!["reason"], Is.EqualTo("expected to fail"));
        var fixedSince = ExpectedFailureReport.ByTitle(report, nameof(DescribeFailFixture.Fixed_since));
        Assert.That((string?)fixedSince["status"], Is.EqualTo("failed"));
        Assert.That((string?)fixedSince["attempts"]![0]!["error"]!["code"], Is.EqualTo("UNEXPECTED_PASS"));
        var card = ExpectedFailureReport.ByTitle(report, nameof(KnownGapsFixture.Card_form));
        Assert.That((string?)card["status"], Is.EqualTo("passed"));
        Assert.That((string?)card["expectedFailure"]!["reason"], Is.EqualTo("old checkout"));
        var outside = ExpectedFailureReport.ByTitle(report, nameof(OutsideTheGroupFixture.Outside_the_group));
        Assert.That((string?)outside["status"], Is.EqualTo("passed"));
        Assert.That(outside["expectedFailure"], Is.Null);
    }

    [Test]
    public void Takes_a_mark_from_a_beforeEach_hook_and_refuses_one_from_an_afterEach_hook()
    {
        Assert.That(
            NestedRun.Run(nameof(MarkedEarlyFixture.Fails_after_a_beforeEach_mark)).ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );

        var late = NestedRun.Run(nameof(MarkedLateFixture.Fails_before_an_afterEach_mark));
        Assert.That(late.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        Assert.That(late.Message, Does.Contain("cannot mark a test from an afterEach hook"));

        var report = ExpectedFailureReport.Read(typeof(MarkedEarlyFixture));
        var early = ExpectedFailureReport.ByTitle(report, nameof(MarkedEarlyFixture.Fails_after_a_beforeEach_mark));
        Assert.That((string?)early["status"], Is.EqualTo("passed"));
        Assert.That((string?)early["expectedFailure"]!["reason"], Is.EqualTo("marked in beforeEach"));
        var lateResult = ExpectedFailureReport.ByTitle(
            report,
            nameof(MarkedLateFixture.Fails_before_an_afterEach_mark)
        );
        Assert.That((string?)lateResult["status"], Is.EqualTo("failed"));
        var lateAttempt = lateResult["attempts"]![0]!;
        Assert.That(lateAttempt["expectedFailure"], Is.Null);
        Assert.That(
            (string?)
                lateAttempt["secondaryErrors"]!.AsArray().Single(error => (string?)error!["phase"] == "afterEach")![
                    "message"
                ],
            Does.Contain("cannot mark a test from an afterEach hook")
        );
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

        var report = ExpectedFailureReport.Read(typeof(ExpectedFailureRetriesFixture));
        var expectedResult = ExpectedFailureReport.ByTitle(
            report,
            nameof(ExpectedFailureRetriesFixture.Expected_failure)
        );
        Assert.That((string?)expectedResult["status"], Is.EqualTo("passed"));
        Assert.That(expectedResult["attempts"]!.AsArray(), Has.Count.EqualTo(1));
        var lateResult = ExpectedFailureReport.ByTitle(
            report,
            nameof(ExpectedFailureRetriesFixture.Fails_before_the_mark)
        );
        Assert.That((string?)lateResult["status"], Is.EqualTo("flaky"));
        Assert.That(
            lateResult["attempts"]!.AsArray().Select(attempt => (string?)attempt!["status"]),
            Is.EqualTo(new[] { "failed", "passed" })
        );
        Assert.That(
            (string?)lateResult["attempts"]![1]!["expectedFailure"]!["reason"],
            Is.EqualTo("gap from the second attempt")
        );
        Assert.That((int)report["run"]!["exitCode"]!, Is.EqualTo(1));
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

        var report = ExpectedFailureReport.Read(typeof(ExpectedFailureSetupFixture));
        var signIn = ExpectedFailureReport.ByTitle(report, "SignIn");
        Assert.That((string?)signIn["attempts"]![0]!["error"]!["code"], Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That((int)report["run"]!["exitCode"]!, Is.EqualTo(1));
    }
}

/// <summary>Reads the <c>report.json</c> a fixture's run wrote (upstream's <c>runProject</c> outcome and <c>assertValidReport</c>).</summary>
internal static class ExpectedFailureReport
{
    public static JsonNode Read(Type fixture)
    {
        var report = JsonNode.Parse(File.ReadAllText(ExpectedFailureResults.ReportOf(fixture)))!;
        E2E.Tests.ReportSchema.AssertValidReport(report);
        return report;
    }

    public static JsonNode ByTitle(JsonNode report, string title) =>
        report["run"]!["results"]!
            .AsArray()
            .Single(result =>
                ((string?)result!["titlePath"]!.AsArray().Last())!.EndsWith("." + title, StringComparison.Ordinal)
            )!;
}
