// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Cli.Tests.ReportList.ListReporterKit;
using AttemptStatus = E2E.Internal.AttemptStatus;

namespace E2E.Cli.Tests.ListReporter;

/// <summary>
/// Upstream: packages/e2e/tests/integration/list-reporter.test.ts, describe('list reporter output'). The first <c>it</c>, the tally of a <c>--repeat-each</c> run, needs the runner and is not ported; <c>ReportList/RepeatsRowTests</c> tests that row from events.
/// </summary>
public sealed class ListReporterOutputTests
{
    // Partial: upstream runs a project through the built runner with a fake engine whose attempt end throws, and also checks the exit code (3).
    // The port has no runner, so the test hands the reporter the result such a run records: a passed test whose attempt's cleanup failed, with
    // the engine's cleanup-phase error beside it.
    [Fact]
    public void Partial_Shows_a_cleanup_failure_under_a_passed_test_and_in_the_summary_not_a_green_summary_with_exit_3()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/close-fail.e2e.ts", 1)));
        var closeFailed = Attempt() with
        {
            Cleanup = AttemptCleanup.Failed,
            SecondaryErrors = [new ReportError("ENGINE_FAILURE", "attempt end failed: close exploded", "cleanup")],
        };
        reporter.Handle(Finished(Result(ResultStatus.Passed, file: "tests/close-fail.e2e.ts", title: ["taps a node"], attempts: [closeFailed])));
        reporter.Handle(RunFinished(ReportRunStatus.Failed, 3, ".e2e/report.json"));

        Assert.Contains(output.Lines, line => line.Contains("✓ |chromium| tests/close-fail.e2e.ts > taps a node", StringComparison.Ordinal));
        Assert.Contains("     → cleanup failed: ENGINE_FAILURE: attempt end failed: close exploded", output.Lines);
        Assert.Contains("    Cleanup  1 cleanup failure: ENGINE_FAILURE: attempt end failed: close exploded", output.Lines);
    }
}
