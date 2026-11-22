// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.NUnit.Tests.SkipAfterFailure;
using NUnit.Framework.Interfaces;

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
