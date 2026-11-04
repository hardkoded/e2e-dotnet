// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Partial port of upstream's <c>tests/integration/list-reporter.test.ts</c>, <c>tallies a --repeat-each run</c>, which runs a project through the
/// built runner. The port has no runner, so this feeds the reporter the events such a run emits and checks the <c>Repeats</c> row only.
/// </summary>
public sealed class RepeatsRowTests
{
    [Fact]
    public void Partial_Names_the_tests_that_missed_a_repeat_and_the_runs_that_failed()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/checkout.e2e.ts", 6)));
        var failed = Attempt(error: new ReportError("ASSERTION_FAILED", "no"), status: AttemptStatus.Failed);
        foreach (var repeat in new[] { 0, 1, 2 })
        {
            reporter.Handle(Finished(Result(repeat == 1 ? ResultStatus.Failed : ResultStatus.Passed, id: "pays", repeat: repeat, title: ["pays"], file: "tests/checkout.e2e.ts", attempts: [repeat == 1 ? failed : Attempt()])));
            reporter.Handle(Finished(Result(ResultStatus.Passed, id: "refunds", repeat: repeat, title: ["refunds"], file: "tests/checkout.e2e.ts", declarationIndex: 1)));
        }

        reporter.Handle(RunFinished(ReportRunStatus.Failed, 1));
        var row = output.Lines.IndexOf(output.Lines.Single(line => line.TrimStart().StartsWith("Repeats  ", StringComparison.Ordinal)));
        Assert.EndsWith("Repeats  1 of 2 tests passed all 3 runs", output.Lines[row]);
        Assert.EndsWith("× |chromium| tests/checkout.e2e.ts > pays  2/3 passed · repeat 1 ASSERTION_FAILED", output.Lines[row + 1]);
        Assert.DoesNotContain(output.Lines.Skip(row + 2).Take(1), line => line.Contains("refunds", StringComparison.Ordinal));
    }

    [Fact]
    public void Prints_no_row_for_a_run_that_repeated_nothing()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(Finished(Result(ResultStatus.Passed)));
        reporter.Handle(RunFinished());
        Assert.DoesNotContain(output.Lines, line => line.Contains("Repeats", StringComparison.Ordinal));
    }
}
