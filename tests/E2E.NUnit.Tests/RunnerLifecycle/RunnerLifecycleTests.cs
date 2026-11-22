// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.NUnit.Tests.EngineContract;
using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace E2E.NUnit.Tests.RunnerLifecycle;

/// <summary>
/// The fixme <c>it</c>s of upstream's <c>tests/integration/runner-lifecycle.test.ts</c>. The first is <c>Partial_</c>: it leaves out the registered form
/// <c>test.fixme(title, fn)</c> (the port has no registration API; a host attribute skips a test as <c>explicit</c>), the setup test that may not skip
/// (a setup test needs a session consumer and the run's exit code), and reads each result from the fixture's <c>report.json</c>.
/// </summary>
public sealed class RunnerLifecycleTests
{
    [OneTimeSetUp]
    public void ClearResults() => RunnerLifecycleResults.Clear();

    [OneTimeTearDown]
    public void RemoveResults() => RunnerLifecycleResults.Clear();

    [Test]
    public void Partial_Reports_test_fixme_tests_skipped_with_the_reason_fixme_from_a_body_or_hook()
    {
        string[] tests =
        [
            nameof(FixmeFixture.Conditional),
            nameof(FixmeFixture.Condition_false_runs_on),
            nameof(FixmeFixture.Bare),
            nameof(FixmeFixture.Blank_reason),
        ];
        var verdicts = tests.ToDictionary(test => test, test => NestedRun.Run(test));
        var hooked = NestedRun.Run(nameof(FixmeFromHookFixture.Member));
        Assert.That(
            verdicts[nameof(FixmeFixture.Condition_false_runs_on)].ResultState.Status,
            Is.EqualTo(TestStatus.Passed)
        );
        Assert.That(hooked.ResultState.Status, Is.EqualTo(TestStatus.Skipped), hooked.Message);

        var report = Read(typeof(FixmeFixture));
        var conditional = ByTitle(report, nameof(FixmeFixture.Conditional));
        Assert.That((string?)conditional["status"], Is.EqualTo("skipped"));
        AssertSkip(conditional["skip"]!, "fixme: flaky on webkit");
        Assert.That(
            conditional["attempts"]![0]!["steps"]!.AsArray().Select(step => (string?)step!["api"]),
            Is.EqualTo(new[] { "app.open" })
        );
        Assert.That(
            (string?)ByTitle(report, nameof(FixmeFixture.Condition_false_runs_on))["status"],
            Is.EqualTo("passed")
        );
        AssertSkip(ByTitle(report, nameof(FixmeFixture.Bare))["skip"]!, "fixme");
        AssertSkip(ByTitle(report, nameof(FixmeFixture.Blank_reason))["skip"]!, "fixme");

        var member = ByTitle(Read(typeof(FixmeFromHookFixture)), nameof(FixmeFromHookFixture.Member));
        AssertSkip(member["skip"]!, "fixme: hook says not yet");
    }

    [Test]
    public void Keeps_the_fixme_cause_beside_a_failure_that_outranks_the_skip()
    {
        var verdict = NestedRun.Run(nameof(FixmeBesideFailureFixture.Beside_a_failure));
        Assert.That(verdict.ResultState.Status, Is.EqualTo(TestStatus.Failed));
        var member = ByTitle(
            Read(typeof(FixmeBesideFailureFixture)),
            nameof(FixmeBesideFailureFixture.Beside_a_failure)
        );
        Assert.That((string?)member["status"], Is.EqualTo("failed"));
        AssertSkip(member["attempts"]![0]!["skip"]!, "fixme: not ready");
        Assert.That((string?)member["attempts"]![0]!["status"], Is.EqualTo("failed"));
    }

    [Test]
    public void Partial_Counts_a_serial_member_that_marks_itself_fixme_as_executed_with_the_cause_fixme()
    {
        var results = Task.Run(() =>
            {
                var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
                runner.Load(typeof(FixmeSerialFixture).Assembly, new Dictionary<string, object>());
                var run = runner.Run(
                    TestListener.NULL,
                    TestFilter.FromXml($"<filter><class>{typeof(FixmeSerialFixture).FullName}</class></filter>")
                );
                return Leaves(run).ToDictionary(result => result.Test.MethodName!);
            })
            .GetAwaiter()
            .GetResult();

        // The member skips itself and the group runs on. The report of a serial group is one result for the group (hardkoded/e2e-dotnet#1241),
        // so the per-member cause fixme and the executed count are not checked here.
        Assert.That(
            results[nameof(FixmeSerialFixture.A_step_1_is_not_ready)].ResultState.Status,
            Is.EqualTo(TestStatus.Skipped)
        );
        Assert.That(
            results[nameof(FixmeSerialFixture.B_step_2_runs)].ResultState.Status,
            Is.EqualTo(TestStatus.Passed),
            results[nameof(FixmeSerialFixture.B_step_2_runs)].Message
        );
    }

    [Test]
    public void Rejects_Fixme_in_a_setup_test_because_its_sessions_are_owed_to_consumers()
    {
        // A setup test is named by its title.
        var results = NestedSessionRun.Run(
            "mark a setup fixme",
            nameof(FixmeSetupFixture.Consumes_the_fixme_setup_session)
        );
        var setup = results["mark a setup fixme"];
        Assert.That(setup.ResultState.Status, Is.EqualTo(TestStatus.Failed), setup.Message);
        Assert.That(setup.Message, Does.Contain("Fixme() cannot skip a setup test"));
    }

    private static IEnumerable<ITestResult> Leaves(ITestResult result) =>
        result.HasChildren ? result.Children.SelectMany(Leaves) : [result];

    private static void AssertSkip(JsonNode skip, string reason)
    {
        Assert.That((string?)skip["cause"], Is.EqualTo("fixme"));
        Assert.That((string?)skip["reason"], Is.EqualTo(reason));
    }

    private static JsonNode Read(Type fixture)
    {
        var report = JsonNode.Parse(File.ReadAllText(RunnerLifecycleResults.ReportOf(fixture)))!;
        E2E.Tests.ReportSchema.AssertValidReport(report);
        return report;
    }

    private static JsonNode ByTitle(JsonNode report, string title) =>
        report["run"]!["results"]!
            .AsArray()
            .Single(result =>
                ((string?)result!["titlePath"]!.AsArray().Last())!.EndsWith("." + title, StringComparison.Ordinal)
            )!;
}
