// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit.Tests.SkipAfterFailure;
using E2E.Reports;
using NUnit.Framework.Interfaces;

namespace E2E.NUnit.Tests.ExpectedFailure;

/// <summary>
/// <c>describe('test.fail')</c> of upstream's <c>tests/integration/expected-failure.test.ts</c>, whose tests run the CLI over a project. These run
/// <see cref="ExpectedFailureFixture"/> through NUnit, with <c>[Property("E2E.Fail", reason)]</c> for the <c>fail</c> option and a property set in the
/// body for <c>test.fail(...)</c>. The first <c>it</c> is <c>Partial_</c>: it checks the verdict <c>dotnet test</c> reports and the JUnit text of the report the run wrote (<c>failed as expected: known gap</c> and
/// <c>type="UNEXPECTED_PASS"</c>), and not the rest of the report
/// (<c>expectedFailure</c> on the result and the attempt, the error's code and category, the summary, or the exit code), and
/// leaves out "engine failure", "times out", and "soft failure then skipped" (no fixture here). Not ported: "takes a mark from a beforeEach
/// hook and refuses one from an afterEach hook" (no <c>test.fail</c> call, so no hook to mark from or refuse), and "does not retry an expected
/// failure, retries an unexpected pass, and a failure before the mark retries as usual" (the port has no retry policy of its own).
/// </summary>
public sealed class TestFailTests
{
    [Test]
    public async Task Partial_passes_a_marked_test_that_fails_fails_one_that_passes_and_keeps_a_skip()
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

        // The JUnit text rendered from the report the run wrote: the cases of this fixture only, since the process's report holds every test that ran.
        var report = await ReportOfFixtureAsync();
        var junit = JunitReport.RenderJunitReport(report);
        Assert.That(junit, Does.Contain("failed as expected: known gap"));
        Assert.That(junit, Does.Contain("type=\"UNEXPECTED_PASS\""));
    }

    // The port names a test by its full NUnit name and reports no file for it.
    private static bool IsOfFixture(E2E.Reports.ReportResult result, string? fixture) =>
        result.TitlePath.Any(title => title.StartsWith(fixture + ".", StringComparison.Ordinal));

    private static async Task<Report> ReportOfFixtureAsync()
    {
        var file = typeof(ExpectedFailureFixture).FullName;
        var runs = await ReportFile.FinishedRunsAsync();
        var found = runs.FirstOrDefault(finished =>
            finished.Report.Run.Results.Any(result => IsOfFixture(result, file))
        )?.Report;
        Assert.That(found, Is.Not.Null, "the process wrote no report with the results of the fixture");
        var run = found!;
        return run with
        {
            Run = run.Run with { Results = [.. run.Run.Results.Where(result => IsOfFixture(result, file))] },
        };
    }
}
