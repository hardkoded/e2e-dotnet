// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.ReportStatus;

/// <summary>
/// The helpers of upstream's <c>tests/unit/report-status.test.ts</c>. Upstream's <c>build</c> also asserts that the document is valid
/// report-1; the port does not write the wire form yet, so the tests here check the records.
/// </summary>
internal static class ReportStatusBuilders
{
    public static readonly ReportError Blocked = new("STEP_BUDGET_EXHAUSTED", "the agent ran out of model calls");

    public static readonly ReportError ProductFailure = new("ASSERTION_FAILED", "expected the dashboard");

    public static readonly ReportError ReportWriteFailure = new("REPORT_WRITE_FAILED", "the JUnit report could not be written: EACCES", "report");

    /// <summary>A failed result for one test titled <paramref name="title"/>; two in one report need distinct titles, as a run gives each result its own id.</summary>
    public static ReportResult FailedResult(ReportError error, string title = "case") =>
        new()
        {
            Id = "result-" + title,
            File = "tests/case.e2e.ts",
            TitlePath = ["suite", title],
            Status = ResultStatus.Failed,
            Attempts = [new ReportAttempt { Status = AttemptStatus.Failed, DurationMs = 10, Error = error }],
        };

    public static Report1Document Build(
        IReadOnlyList<ReportResult>? results = null,
        ReportRunStatus status = ReportRunStatus.Failed,
        int exitCode = 1,
        IReadOnlyList<ReportError>? runErrors = null,
        string? projectRoot = null) =>
        BuildReport.Build(new BuildReportOptions
        {
            RunId = "run-1",
            StartedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Status = status,
            ExitCode = exitCode,
            Results = results ?? [],
            RunErrors = runErrors ?? [],
            ProjectRoot = projectRoot,
        });
}
