// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E.GitHub.Tests;

/// <summary>
/// Upstream's <c>fixtures.ts</c>: report documents for the tests. Upstream grows them from the runner's own valid report-1 fixture; these build the
/// public <see cref="Report"/> tree directly, from a few inputs.
/// </summary>
internal static class Fixtures
{
    public static ReportAttempt Attempt(
        AttemptStatus status = AttemptStatus.Passed,
        long durationMs = 1200,
        ReportError? error = null,
        IReadOnlyList<ArtifactKind>? artifacts = null) =>
        new()
        {
            Id = "attempt-1",
            Status = status,
            StartedAt = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero),
            DurationMs = durationMs,
            Artifacts = (artifacts ?? []).Select((kind, index) => new ArtifactRecord
            {
                Id = $"attempt-1:artifact:{index}",
                Kind = kind,
                MediaType = kind == ArtifactKind.Video ? "video/webm" : "image/png",
                Path = $"attempt-1/{index}.{(kind == ArtifactKind.Video ? "webm" : "png")}",
            }).ToList(),
            Error = error,
        };

    public static ReportError Error(string code, string message) => new() { Category = ErrorCategory.Test, Code = code, Message = message };

    public static ReportResult Result(
        string title,
        ResultStatus status,
        string file = "tests/example.e2e.ts",
        int line = 3,
        string target = "web",
        IReadOnlyList<ReportAttempt>? attempts = null,
        bool selected = true,
        SkipInfo? skip = null,
        int repeat = 0)
    {
        var testId = $"{file}::{Uri.EscapeDataString(title)}";
        return new()
        {
            // One id per test, target, and repeat, the way the runner names a result, so a fold by id tells them apart.
            Id = $"{testId}@{target}{(repeat == 0 ? "" : $"#{repeat}")}",
            TestId = testId,
            TitlePath = [title],
            Repeat = repeat,
            File = file,
            Source = new ReportSource { File = file, Line = line, Column = 1 },
            TargetId = target,
            Platform = "web",
            Agent = "default",
            Selected = selected,
            Status = status,
            Skip = skip,
            Attempts = attempts ?? [],
        };
    }

    public static Report ReportOf(
        ReportRunStatus status = ReportRunStatus.Passed,
        IReadOnlyList<ReportResult>? results = null,
        string projectId = "dev.example.shop",
        ReportExplore? explore = null,
        IReadOnlyList<ReportError>? errors = null,
        ReportCarried? carried = null,
        IReadOnlyList<ReportFilter>? filters = null,
        ReportChanges? changes = null) =>
        new()
        {
            Run = new ReportRun
            {
                Id = "run-1",
                Runner = new ReportRunner { Name = "e2e", Version = "0.9.0" },
                Status = status,
                ExitCode = status == ReportRunStatus.Passed ? 0 : 1,
                StartedAt = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero),
                FinishedAt = new DateTimeOffset(2026, 7, 24, 12, 0, 8, 400, TimeSpan.Zero),
                Project = new ReportProject { Id = projectId },
                Targets = [new ReportTarget { Id = "web", Index = 0, Platform = "web" }],
                Results = results ?? [],
                Errors = errors ?? [],
                Carried = carried,
                Filters = filters,
                Changes = changes,
                Explore = explore,
            },
        };

    public static FinishedRun Finished(Report report, Report? lastRun = null) =>
        new(report, report.Run.Status, report.Run.ExitCode, "/work/app", "/work/app/.e2e/report.json", "/work/app/.e2e/artifacts") { LastRun = lastRun };
}
