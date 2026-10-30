// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Cli.Report;
using E2E.Internal;

namespace E2E.Cli.Tests.ReportList;

/// <summary>The helpers upstream's <c>unit/report-list.test.ts</c> declares at the top of the file.</summary>
internal static partial class ListReporterKit
{
    /// <summary>Upstream's <c>capture()</c>: the lines a reporter wrote, with their colors stripped.</summary>
    public sealed partial class Capture : IListReporterOutput
    {
        public List<string> Lines { get; } = [];

        public void Write(string line) => Lines.Add(Ansi().Replace(line, string.Empty));

        [GeneratedRegex("\u001b\\[[0-9;?]*[a-zA-Z]")]
        private static partial Regex Ansi();
    }

    /// <summary>Upstream's <c>plainReporter()</c>: a reporter with colors off, so assertions read the plain text.</summary>
    public static ListReporter PlainReporter(IListReporterOutput output, TimeProvider? time = null) =>
        new(output, new ListReporterOptions { Colors = false }, time);

    /// <summary>Upstream's <c>attempt()</c>.</summary>
    public static ReportAttempt Attempt(IReadOnlyList<StepRecord>? steps = null, ReportError? error = null, AttemptStatus status = AttemptStatus.Passed) =>
        new()
        {
            Id = "attempt-1",
            StartedAt = DateTimeOffset.UnixEpoch,
            DurationMs = 120,
            Status = status,
            Error = error,
            Steps = steps ?? [],
        };

    /// <summary>Upstream's <c>result()</c>.</summary>
    public static RunEventResult Result(
        ResultStatus status,
        string[]? title = null,
        string id = "test-1",
        string file = "tests/case.e2e.ts",
        int declarationIndex = 0,
        string target = "chromium",
        bool selected = true,
        IReadOnlyList<ReportAttempt>? attempts = null,
        string? skipReason = null,
        string agent = "default",
        int repeat = 0) =>
        new()
        {
            Test = new TestIdentity(title ?? ["suite", "case"], id, file, declarationIndex),
            Target = (target, "web"),
            ResultId = id,
            Agent = agent,
            Repeat = repeat,
            Status = status,
            Selected = selected,
            Skip = skipReason is null ? null : new ReportSkip("explicit", skipReason),
            Attempts = attempts ?? [Attempt()],
        };

    /// <summary>Upstream's <c>runStarted()</c>.</summary>
    public static RunStartedEvent RunStarted(bool ci = false, string[]? targets = null, string projectRoot = "/project", string? model = null, string? judge = null) =>
        new()
        {
            RunId = "run-1",
            ProjectId = "project",
            ProjectRoot = projectRoot,
            ArtifactsRoot = projectRoot + "/.e2e/artifacts",
            Ci = ci,
            Targets = targets ?? ["chromium"],
            Model = model,
            Judge = judge,
        };

    /// <summary>Upstream's <c>plan()</c>.</summary>
    public static PlanEvent Plan(params (string File, int Tests)[] files)
    {
        var planned = files.Select(entry => (entry.File, "chromium", entry.Tests)).ToList();
        return new PlanEvent(planned.Sum(entry => entry.Tests), planned);
    }

    /// <summary>Upstream's <c>finished()</c>.</summary>
    public static TestFinishedEvent Finished(RunEventResult result) => new(result);

    /// <summary>Upstream's <c>runFinished()</c>.</summary>
    public static RunFinishedEvent RunFinished(ReportRunStatus status = ReportRunStatus.Passed, int exitCode = 0, string? reportPath = null, string? aiTracePath = null) =>
        new() { Status = status, ExitCode = exitCode, ReportPath = reportPath, AiTracePath = aiTracePath };

    /// <summary>A <see cref="TimeProvider"/> that stands still until it is advanced, for upstream's <c>vi.useFakeTimers</c>.</summary>
    public sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan span) => _now += span;
    }
}
