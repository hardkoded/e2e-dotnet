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
    public static ReportAttempt Attempt(IReadOnlyList<StepRecord>? steps = null, ReportError? error = null, AttemptStatus status = AttemptStatus.Passed, string id = "attempt-1") =>
        new()
        {
            Id = id,
            StartedAt = DateTimeOffset.UnixEpoch,
            DurationMs = 120,
            Status = status,
            Error = error,
            Steps = steps ?? [],
        };

    /// <summary>Upstream's <c>failedAttempt()</c>.</summary>
    public static ReportAttempt FailedAttempt(string message) =>
        Attempt(error: new ReportError("ASSERTION_FAILED", message), status: AttemptStatus.Failed);

    /// <summary>Upstream's <c>testStarted()</c>.</summary>
    public static TestStartedEvent TestStarted(string testId, string title, string target, string file = "tests/case.e2e.ts", string agent = "default", string? serialId = null) =>
        new(testId, agent, 0, title, file, serialId, target);

    /// <summary>The <c>step</c> event of the default agent's first run of <paramref name="testId"/>, on <c>chromium</c>.</summary>
    public static StepProgressEvent Step(string testId, StepProgress progress, string agent = "default") => new(testId, agent, 0, "chromium", progress);

    /// <summary>Upstream's <c>agentStep()</c> of <c>describe('step streaming')</c>: an agent step's <c>start</c> or <c>end</c> progress.</summary>
    public static StepProgress AgentStep(StepProgressPhase phase, string label, StepStatus status = StepStatus.Passed) =>
        phase == StepProgressPhase.Start
            ? new StepProgress { Phase = phase, Kind = StepKind.Agent, Api = "agent.act", Label = label }
            : new StepProgress { Phase = phase, Kind = StepKind.Agent, Api = "agent.act", Label = label, Status = status, DurationMs = 4_200, ModelCalls = 3 };

    /// <summary>Upstream's <c>agentStepRecord()</c>: a finished <c>agent.act</c> step record that carries <paramref name="cache"/>.</summary>
    public static StepRecord AgentStepRecord(string label, CacheInfo? cache = null, string? agent = null, int index = 0) =>
        new()
        {
            Id = label,
            Index = index,
            Kind = StepKind.Agent,
            Api = "agent.act",
            Label = label,
            StartedAt = DateTimeOffset.UnixEpoch,
            DurationMs = 10,
            Cache = cache,
            Agent = agent,
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
