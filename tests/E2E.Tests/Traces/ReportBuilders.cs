// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Traces;

/// <summary>
/// Upstream's <c>tests/helpers/report.ts</c>: builders for the report a trace page reads. Every field has a plain default, and
/// the arguments replace the ones a test cares about, so a test states only what it is about.
/// </summary>
internal static class ReportBuilders
{
    public static readonly DateTimeOffset ReportAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly SourceLocation Origin = new("tests/auth.e2e.ts", 3, 1);

    public static ReportError Error(string code = "ASSERTION_FAILED", string message = "expected \"Sign in\" to be visible", string? phase = null, SourceLocation? source = null, ReportErrorDetails? details = null) =>
        new(code, message, phase, source, details);

    public static StepRecord Step(
        int index = 0,
        StepKind kind = StepKind.Locator,
        string api = "locator.tap",
        string label = "tap Sign in",
        StepStatus status = StepStatus.Passed,
        IEnumerable<StepEvent>? events = null,
        CacheInfo? cache = null,
        CacheWrite? cacheWrite = null,
        StepScreen? screen = null,
        SerializedError? error = null)
    {
        var step = new StepRecord
        {
            Id = "step-" + index,
            Index = index,
            Kind = kind,
            Api = api,
            Label = label,
            Source = Origin,
            Status = status,
            StartedAt = ReportAt,
            DurationMs = 100,
            Screen = screen,
            Cache = cache,
            CacheWrite = cacheWrite,
            Error = error,
        };
        step.Events.AddRange(events ?? []);
        return step;
    }

    public static ReportAttempt Attempt(
        AttemptStatus status = AttemptStatus.Passed,
        ReportError? error = null,
        IReadOnlyList<ReportError>? secondaryErrors = null,
        IReadOnlyList<AppLogRecord>? appLog = null,
        IReadOnlyList<StepRecord>? steps = null,
        IReadOnlyDictionary<string, string>? environment = null) =>
        new()
        {
            Status = status,
            DurationMs = 1234,
            Error = error,
            SecondaryErrors = secondaryErrors ?? [],
            AppLog = appLog ?? [],
            Steps = steps ?? [],
            Environment = environment,
        };

    public static ReportResult Result(
        string id = "result-1",
        string file = "tests/auth.e2e.ts",
        IReadOnlyList<string>? titlePath = null,
        ResultStatus status = ResultStatus.Passed,
        IReadOnlyList<ReportAttempt>? attempts = null) =>
        new()
        {
            Id = id,
            File = file,
            TitlePath = titlePath ?? ["auth", "signs in"],
            Source = Origin,
            Status = status,
            Attempts = attempts ?? [Attempt()],
        };

    /// <summary>A whole document of one run holding <paramref name="results"/>.</summary>
    public static Report1Document Document(params ReportResult[] results) =>
        new(new ReportRun("run-1", "0.0.0", [], results));

    public static StepEvent Event(StepEventKind kind, DateTimeOffset startedAt, long durationMs = 1, StepEventStatus status = StepEventStatus.Passed, string? name = null, int? count = null, string? code = null, string? detail = null) =>
        new() { Kind = kind, StartedAt = startedAt, DurationMs = durationMs, Status = status, Name = name, Count = count, Code = code, Detail = detail };
}
