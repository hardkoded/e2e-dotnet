// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.TUnit.Tests.EngineContract;

namespace E2E.TUnit.Tests.RunnerLifecycle;

/// <summary>
/// The fixme <c>it</c>s of upstream's <c>tests/integration/runner-lifecycle.test.ts</c>. Each result is read from the fixture's <c>report.json</c>.
/// </summary>
public sealed class RunnerLifecycleTests
{
    [After(Class)]
    public static void RemoveResults() => RunnerLifecycleResults.Clear();

    [Test]
    public async Task Reports_test_fixme_tests_skipped_with_the_reason_fixme_registered_or_from_a_body()
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
        await Assert.That(registered["attempts"]!.AsArray().Count).IsEqualTo(0);
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

        var (hookResults, _) = await NestedSessionRun.RunAsync(typeof(FixmeFromHookFixture));
        await Assert.That(hookResults[nameof(FixmeFromHookFixture.Member)].Result).IsEqualTo("Skipped");
        var hooked = JsonNode.Parse(File.ReadAllText(RunnerLifecycleResults.ReportOf(typeof(FixmeFromHookFixture))))!;
        await AssertSkip(ByTitle(hooked, nameof(FixmeFromHookFixture.Member))["skip"]!, "fixme: hook says not yet");

        var (setupResults, _) = await NestedSessionRun.RunAsync(typeof(FixmeInSetupFixture));
        var setup = setupResults[nameof(FixmeInSetupFixture.SignIn)];
        await Assert.That(setup.Result).IsEqualTo("Failed");
        await Assert.That(setup.Message).Contains("Fixme() cannot skip a setup test");
        await Assert.That(setupResults[nameof(FixmeInSetupFixture.Consumer)].Result).IsEqualTo("Skipped");
        var setupReport = JsonNode.Parse(
            File.ReadAllText(RunnerLifecycleResults.ReportOf(typeof(FixmeInSetupFixture)))
        )!;
        var error = ByTitle(setupReport, nameof(FixmeInSetupFixture.SignIn))["attempts"]![0]!["error"]!;
        await Assert.That((string?)error["code"]).IsEqualTo("INVALID_ARGUMENT");
        await Assert.That((string?)error["message"]).Contains("Fixme() cannot skip a setup test");
        var consumer = ByTitle(setupReport, nameof(FixmeInSetupFixture.Consumer));
        await Assert.That((string?)consumer["status"]).IsEqualTo("skipped");
        await Assert.That((string?)consumer["skip"]!["cause"]).IsEqualTo("setup-failed");
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
