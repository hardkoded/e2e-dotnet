// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, ListReporter describe('failed tests section'). Not ported: <c>lists a retry that
/// skipped with the failed attempt’s error and evidence</c>, because the port has no <c>Skipped After Failure</c> section (issue #843).
/// </summary>
public sealed class FailedTestsSectionTests
{
    /// <summary>Upstream's <c>failedAttempt()</c>.</summary>
    private static ReportAttempt FailedAttempt(string message) =>
        Attempt(error: new ReportError("ASSERTION_FAILED", message), status: AttemptStatus.Failed);

    [Fact]
    public void Quotes_the_first_app_errors_under_the_tally_loopback_origins_dropped_and_counts_the_rest()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        var failed = FailedAttempt("locator matched 2 nodes");
        DateTimeOffset At(int second) => new(2026, 1, 1, 0, 0, second, TimeSpan.Zero);
        failed = failed with
        {
            AppLog =
            [
                new AppLogRecord(AppLogSource.Console, AppLogLevel.Error, "late fallout", At(4), 0),
                new AppLogRecord(AppLogSource.Console, AppLogLevel.Warning, "each child needs a key", At(0), 0),
                new AppLogRecord(AppLogSource.Network, AppLogLevel.Error, "GET http://127.0.0.1:4271/api/me 500", At(3), 0),
                new AppLogRecord(AppLogSource.Console, AppLogLevel.Error, "Error occurred prerendering page \"/profile\"", At(1), 0),
                new AppLogRecord(AppLogSource.Error, AppLogLevel.Error, "TypeError: x is undefined", At(2), 0),
                new AppLogRecord(AppLogSource.Console, AppLogLevel.Info, "ready", At(5), 0),
            ],
        };
        reporter.Handle(Finished(Result(ResultStatus.Failed, attempts: [failed])));
        reporter.Handle(RunFinished(ReportRunStatus.Failed, 1));
        var app = output.Lines.IndexOf(" ❯ app 4 errors, 1 warning logged");
        Assert.True(app > -1);
        Assert.Equal(
            [
                "   × console error: Error occurred prerendering page \"/profile\"",
                "   × uncaught error: TypeError: x is undefined",
                "   × network error: GET /api/me 500",
                "   1 more app error logged",
            ],
            output.Lines.Skip(app + 1).Take(4));
    }

    [Fact]
    public void Names_the_status_when_a_failure_recorded_no_error()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(Finished(Result(ResultStatus.TimedOut, attempts: [Attempt(status: AttemptStatus.TimedOut)])));
        reporter.Handle(RunFinished(ReportRunStatus.Failed, 1));
        Assert.Contains("timed-out: no error was recorded", output.Lines);
    }
}
