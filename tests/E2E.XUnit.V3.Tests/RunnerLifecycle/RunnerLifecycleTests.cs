// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.XUnit.V3.Tests.EngineContract;

namespace E2E.XUnit.V3.Tests.RunnerLifecycle;

/// <summary>
/// The fixme <c>it</c> of upstream's <c>tests/integration/runner-lifecycle.test.ts</c>. It is <c>Partial_</c>: it leaves out the registered form
/// <c>test.fixme(title, fn)</c> (the port has no registration API) and the setup test that may not skip, and reads each result from the fixture's
/// <c>report.json</c>.
/// </summary>
public sealed class RunnerLifecycleTests : IDisposable
{
    public void Dispose() => RunnerLifecycleResults.Clear();

    [Fact]
    public async Task Partial_Reports_test_fixme_tests_skipped_with_the_reason_fixme_from_a_body()
    {
        // Choosing the directory before the nested process starts is what hands it to that process.
        RunnerLifecycleResults.Clear();
        var (results, _) = await NestedSessionRun.RunAsync(typeof(FixmeFixture));
        Assert.Equal("Skip", results[nameof(FixmeFixture.Conditional)].Result);
        Assert.Equal("Pass", results[nameof(FixmeFixture.Condition_false_runs_on)].Result);

        var report = JsonNode.Parse(File.ReadAllText(RunnerLifecycleResults.ReportOf(typeof(FixmeFixture))))!;
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
