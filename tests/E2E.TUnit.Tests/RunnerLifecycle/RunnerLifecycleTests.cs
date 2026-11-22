// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.TUnit.Tests.EngineContract;

namespace E2E.TUnit.Tests.RunnerLifecycle;

/// <summary>
/// The fixme <c>it</c>s of upstream's <c>tests/integration/runner-lifecycle.test.ts</c>. It is <c>Partial_</c>: it leaves out that the registered form
/// <c>[Fixme]</c> (upstream's <c>test.fixme(title, fn)</c>) has no attempt (it starts its session before it skips), the consumer's <c>setup-failed</c> skip, and reads each result from the fixture's
/// <c>report.json</c>.
/// </summary>
public sealed class RunnerLifecycleTests
{
    [After(Class)]
    public static void RemoveResults() => RunnerLifecycleResults.Clear();

    [Test]
    public async Task Partial_Reports_test_fixme_tests_skipped_with_the_reason_fixme_registered_or_from_a_body()
    {
        // Choosing the directory before the nested process starts is what hands it to that process.
        RunnerLifecycleResults.Prepare();
        var (results, _) = await NestedSessionRun.RunAsync(typeof(FixmeFixture));
        await Assert.That(results[nameof(FixmeFixture.Registered)].Result).IsEqualTo("Skipped");
        await Assert.That(results[nameof(FixmeFixture.Conditional)].Result).IsEqualTo("Skipped");
        await Assert.That(results[nameof(FixmeFixture.Condition_false_runs_on)].Result).IsEqualTo("Passed");

        var report = JsonNode.Parse(File.ReadAllText(RunnerLifecycleResults.ReportOf(typeof(FixmeFixture))))!;
        var registered = ByTitle(report, nameof(FixmeFixture.Registered));
        await Assert.That((string?)registered["status"]).IsEqualTo("skipped");
        await AssertSkip(registered["skip"]!, "fixme");
        var conditional = ByTitle(report, nameof(FixmeFixture.Conditional));
        await Assert.That((string?)conditional["status"]).IsEqualTo("skipped");
        await AssertSkip(conditional["skip"]!, "fixme: flaky on webkit");
        await Assert
            .That(conditional["attempts"]![0]!["steps"]!.AsArray().Select(step => (string)step!["api"]!).ToArray())
            .IsEquivalentTo(["app.open"]);
        await Assert
            .That((string?)ByTitle(report, nameof(FixmeFixture.Condition_false_runs_on))["status"])
            .IsEqualTo("passed");
        await AssertSkip(ByTitle(report, nameof(FixmeFixture.Bare))["skip"]!, "fixme");
        await AssertSkip(ByTitle(report, nameof(FixmeFixture.Blank_reason))["skip"]!, "fixme");
    }

    [Test]
    public async Task Partial_Rejects_test_fixme_in_a_setup_test()
    {
        RunnerLifecycleResults.Prepare();
        var (results, _) = await NestedSessionRun.RunAsync(typeof(FixmeInSetupFixture));
        var setup = results[nameof(FixmeInSetupFixture.SignIn)];
        await Assert.That(setup.Result).IsEqualTo("Failed");
        await Assert.That(setup.Message).Contains("Fixme() cannot skip a setup test");

        var report = JsonNode.Parse(File.ReadAllText(RunnerLifecycleResults.ReportOf(typeof(FixmeInSetupFixture))))!;
        var error = ByTitle(report, nameof(FixmeInSetupFixture.SignIn))["attempts"]![0]!["error"]!;
        await Assert.That((string?)error["code"]).IsEqualTo("INVALID_ARGUMENT");
    }

    [Test]
    public async Task Keeps_the_fixme_cause_beside_a_failure_that_outranks_the_skip()
    {
        RunnerLifecycleResults.Prepare();
        var (results, _) = await NestedSessionRun.RunAsync(typeof(FixmeBesideFailureFixture));
        await Assert.That(results[nameof(FixmeBesideFailureFixture.Beside_a_failure)].Result).IsEqualTo("Failed");
        var member = ByTitle(
            JsonNode.Parse(File.ReadAllText(RunnerLifecycleResults.ReportOf(typeof(FixmeBesideFailureFixture))))!,
            nameof(FixmeBesideFailureFixture.Beside_a_failure)
        );
        await Assert.That((string?)member["status"]).IsEqualTo("failed");
        await AssertSkip(member["attempts"]![0]!["skip"]!, "fixme: not ready");
        await Assert.That((string?)member["attempts"]![0]!["status"]).IsEqualTo("failed");
    }

    private static async Task AssertSkip(JsonNode skip, string reason)
    {
        await Assert.That((string?)skip["cause"]).IsEqualTo("fixme");
        await Assert.That((string?)skip["reason"]).IsEqualTo(reason);
    }

    private static JsonNode ByTitle(JsonNode report, string title) =>
        report["run"]!["results"]!
            .AsArray()
            .Single(result =>
                ((string?)result!["titlePath"]!.AsArray().Last())!.EndsWith("." + title, StringComparison.Ordinal)
            )!;
}
