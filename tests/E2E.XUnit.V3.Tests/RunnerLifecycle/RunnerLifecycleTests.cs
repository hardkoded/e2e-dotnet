// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.XUnit.V3.Tests.EngineContract;

namespace E2E.XUnit.V3.Tests.RunnerLifecycle;

/// <summary>
/// The fixme <c>it</c>s of upstream's <c>tests/integration/runner-lifecycle.test.ts</c>. Each result is read from the fixture's <c>report.json</c>.
/// </summary>
public sealed class RunnerLifecycleTests : IDisposable
{
    public void Dispose() => RunnerLifecycleResults.Clear();

    [Fact]
    public async Task Reports_test_fixme_tests_skipped_with_the_reason_fixme_registered_or_from_a_body()
    {
        // Choosing the directory before the nested process starts is what hands it to that process.
        RunnerLifecycleResults.Prepare();
        var (results, _) = await NestedSessionRun.RunAsync(typeof(FixmeFixture));
        Assert.Equal("Skip", results[nameof(FixmeFixture.Registered)].Result);
        Assert.Equal("Skip", results[nameof(FixmeFixture.Conditional)].Result);
        Assert.Equal("Pass", results[nameof(FixmeFixture.Condition_false_runs_on)].Result);

        var report = JsonNode.Parse(File.ReadAllText(RunnerLifecycleResults.ReportOf(typeof(FixmeFixture))))!;
        var registered = ByTitle(report, nameof(FixmeFixture.Registered));
        Assert.Equal("skipped", (string?)registered["status"]);
        AssertSkip(registered["skip"]!, "fixme");
        Assert.Empty(registered["attempts"]!.AsArray());
        var conditional = ByTitle(report, nameof(FixmeFixture.Conditional));
        Assert.Equal("skipped", (string?)conditional["status"]);
        AssertSkip(conditional["skip"]!, "fixme: flaky on webkit");
        Assert.Equal(
            ["app.open"],
            conditional["attempts"]![0]!["steps"]!.AsArray().Select(step => (string?)step!["api"]).ToArray()
        );
        Assert.Equal("passed", (string?)ByTitle(report, nameof(FixmeFixture.Condition_false_runs_on))["status"]);
        AssertSkip(ByTitle(report, nameof(FixmeFixture.Bare))["skip"]!, "fixme");
        AssertSkip(ByTitle(report, nameof(FixmeFixture.Blank_reason))["skip"]!, "fixme");

        var (setupResults, _) = await NestedSessionRun.RunAsync(typeof(FixmeInSetupFixture));
        Assert.Equal("Fail", setupResults[nameof(FixmeInSetupFixture.SignIn)].Result);
        Assert.Contains("Fixme() cannot skip a setup test", setupResults[nameof(FixmeInSetupFixture.SignIn)].Message);
        Assert.Equal("Skip", setupResults[nameof(FixmeInSetupFixture.Consumer)].Result);
        var setupReport = JsonNode.Parse(
            File.ReadAllText(RunnerLifecycleResults.ReportOf(typeof(FixmeInSetupFixture)))
        )!;
        var error = ByTitle(setupReport, nameof(FixmeInSetupFixture.SignIn))["attempts"]![0]!["error"]!;
        Assert.Equal("INVALID_ARGUMENT", (string?)error["code"]);
        Assert.Contains("Fixme() cannot skip a setup test", (string?)error["message"]);
        var consumer = ByTitle(setupReport, nameof(FixmeInSetupFixture.Consumer));
        Assert.Equal("skipped", (string?)consumer["status"]);
        Assert.Equal("setup-failed", (string?)consumer["skip"]!["cause"]);
    }

    [Fact]
    public async Task Keeps_the_fixme_cause_beside_a_failure_that_outranks_the_skip()
    {
        RunnerLifecycleResults.Prepare();
        var (results, _) = await NestedSessionRun.RunAsync(typeof(FixmeBesideFailureFixture));
        Assert.Equal("Fail", results[nameof(FixmeBesideFailureFixture.Beside_a_failure)].Result);
        var member = ByTitle(
            JsonNode.Parse(File.ReadAllText(RunnerLifecycleResults.ReportOf(typeof(FixmeBesideFailureFixture))))!,
            nameof(FixmeBesideFailureFixture.Beside_a_failure)
        );
        Assert.Equal("failed", (string?)member["status"]);
        AssertSkip(member["attempts"]![0]!["skip"]!, "fixme: not ready");
        Assert.Equal("failed", (string?)member["attempts"]![0]!["status"]);
    }

    private static void AssertSkip(JsonNode skip, string reason)
    {
        Assert.Equal("fixme", (string?)skip["cause"]);
        Assert.Equal(reason, (string?)skip["reason"]);
    }

    private static JsonNode ByTitle(JsonNode report, string title) =>
        report["run"]!["results"]!
            .AsArray()
            .Single(result =>
                ((string?)result!["titlePath"]!.AsArray().Last())!.EndsWith("." + title, StringComparison.Ordinal)
            )!;
}
