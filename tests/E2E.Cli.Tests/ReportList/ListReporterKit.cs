// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Cli.Report;
using E2E.Internal;
using E2E.Reports;
using AttemptStatus = E2E.Internal.AttemptStatus;

namespace E2E.Cli.Tests.ReportList;

/// <summary>The helpers upstream's <c>unit/report-list.test.ts</c> declares at the top of the file.</summary>
internal static partial class ListReporterKit
{
    /// <summary>Upstream's <c>capture()</c>: the lines a reporter wrote, with their colors stripped.</summary>
    public sealed partial class Capture : IListReporterOutput
    {
        public List<string> Lines { get; } = [];

        /// <summary>The raw control channel; null for a sink without one, as upstream's <c>output</c> without <c>raw</c>.</summary>
        public Action<string>? Raw { get; init; }

        public void Write(string line) => Lines.Add(StripAnsi(line));

        public static string StripAnsi(string text) => Ansi().Replace(text, string.Empty);

        [GeneratedRegex("\u001b\\[[0-9;?]*[a-zA-Z]")]
        private static partial Regex Ansi();
    }

    /// <summary>
    /// Upstream's <c>liveCapture()</c> of <c>describe('live window')</c>: the lines written, and every chunk (a line with its newline, or a raw
    /// control write) in the order it was written.
    /// </summary>
    public sealed class LiveCapture : IListReporterOutput
    {
        public LiveCapture() => Raw = Chunks.Add;

        public List<string> Chunks { get; } = [];

        public List<string> Lines { get; } = [];

        public Action<string>? Raw { get; }

        public void Write(string line)
        {
            Lines.Add(Capture.StripAnsi(line));
            Chunks.Add(line + "\n");
        }
    }

    /// <summary>The text of a chunk with its ANSI sequences stripped.</summary>
    public static string StripAnsi(string text) => Capture.StripAnsi(text);

    /// <summary>Upstream's <c>FROZEN_NOW</c>: the instant the golden tests freeze the clock at.</summary>
    public static readonly DateTimeOffset FrozenNow = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Upstream's <c>useGoldenTerminal()</c>: an 80x30 terminal. The zone is UTC and the clock frozen by the <see cref="FakeTime"/> the test builds
    /// its reporter with, so durations, start times, and spinner frames print the same on every machine.
    /// </summary>
    public static IDisposable GoldenTerminal() => Format.WithTerminalSize(rows: 30, columns: 80);

    /// <summary>
    /// Upstream's <c>expectGolden()</c>: compares <paramref name="text"/> with <c>Fixtures/ListReporter/&lt;name&gt;</c>. <c>E2E_GOLDEN_UPDATE=1</c>
    /// rewrites the file in the source tree instead; review the diff like any other change.
    /// </summary>
    public static void ExpectGolden(string name, string text, [System.Runtime.CompilerServices.CallerFilePath] string caller = "")
    {
        if (Environment.GetEnvironmentVariable("E2E_GOLDEN_UPDATE") == "1")
        {
            var directory = Path.Combine(Path.GetDirectoryName(caller)!, "..", "Fixtures", "ListReporter");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), text);
            return;
        }

        var file = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ListReporter", name);
        Assert.True(File.Exists(file), $"{file} is missing; run with E2E_GOLDEN_UPDATE=1");
        Assert.Equal(File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal), text.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    /// <summary>Upstream's <c>stable()</c>: masks what differs between releases and machines, the package version and, when given, a temporary project root.</summary>
    public static string Stable(string text, string? root = null)
    {
        var versioned = Version().Replace(text, " e2e v<version> ");
        return root is null ? versioned : versioned.Replace(root, "/project", StringComparison.Ordinal);
    }

    [GeneratedRegex(@" e2e v\S+ ")]
    private static partial Regex Version();

    /// <summary>Upstream's <c>plainReporter()</c>: a reporter with colors off, so assertions read the plain text.</summary>
    public static global::E2E.Cli.Report.ListReporter PlainReporter(IListReporterOutput output, TimeProvider? time = null, bool live = false) =>
        new(output, new ListReporterOptions { Colors = false, Live = live }, time ?? (live ? new FakeTime(DateTimeOffset.UnixEpoch) : null));

    /// <summary>Upstream's <c>attempt()</c>.</summary>
    public static ReportAttempt Attempt(IReadOnlyList<StepRecord>? steps = null, ReportError? error = null, AttemptStatus status = AttemptStatus.Passed, string id = "attempt-1", long durationMs = 120, IReadOnlyList<ReportArtifact>? artifacts = null) =>
        new()
        {
            Id = id,
            StartedAt = DateTimeOffset.UnixEpoch,
            DurationMs = durationMs,
            Status = status,
            Error = error,
            Steps = steps ?? [],
            Artifacts = artifacts ?? [],
        };

    /// <summary>Upstream's <c>failedAttempt()</c>.</summary>
    public static ReportAttempt FailedAttempt(string message, SourceLocation? source = null) =>
        Attempt(error: new ReportError("ASSERTION_FAILED", message, Source: source), status: AttemptStatus.Failed);

    /// <summary>Upstream's <c>testStarted()</c>.</summary>
    public static TestStartedEvent TestStarted(string testId, string title, string target, string file = "tests/case.e2e.ts", string agent = "default", string? serialId = null) =>
        new(testId, agent, 0, title, file, serialId, target);

    /// <summary>The <c>step</c> event of the default agent's first run of <paramref name="testId"/>, on <c>chromium</c> unless <paramref name="target"/> says otherwise.</summary>
    public static StepProgressEvent Step(string testId, StepProgress progress, string agent = "default", string target = "chromium") => new(testId, agent, 0, target, StepProgressWire.Public(progress));

    /// <summary>Upstream's <c>agentStep()</c> of <c>describe('step streaming')</c>: an agent step's <c>start</c> or <c>end</c> progress.</summary>
    public static StepProgress AgentStep(StepProgressPhase phase, string label, StepStatus status = StepStatus.Passed) =>
        phase == StepProgressPhase.Start
            ? new StepProgress { Phase = phase, Kind = StepKind.Agent, Api = "agent.act", Label = label }
            : new StepProgress { Phase = phase, Kind = StepKind.Agent, Api = "agent.act", Label = label, Status = status, DurationMs = 4_200, ModelCalls = 3 };

    /// <summary>Upstream's <c>agentStepRecord()</c>: a finished <c>agent.act</c> step record that carries <paramref name="cache"/>.</summary>
    public static StepRecord AgentStepRecord(string label, CacheInfo? cache = null, string? agent = null, int index = 0, StepModelInfo? model = null) =>
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
            Model = model,
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
        int repeat = 0,
        int? line = null,
        string? filter = null) =>
        new()
        {
            Test = new TestIdentity(title ?? ["suite", "case"], id, file, declarationIndex),
            Target = (target, "web"),
            ResultId = id,
            Agent = agent,
            Repeat = repeat,
            Status = Enum.Parse<Reports.ResultStatus>(status.ToString()),
            Selected = selected,
            Skip = skipReason is null ? null : new Reports.SkipInfo { Cause = filter is null ? Reports.SkipCause.Explicit : Reports.SkipCause.Filtered, Reason = skipReason, Filter = filter },
            Recorded = new ReportResult
            {
                Id = id,
                File = file,
                TitlePath = title ?? ["suite", "case"],
                Status = status,
                Skip = skipReason is null ? null : new ReportSkip(filter is null ? "explicit" : "filtered", skipReason, filter),
                Source = line is { } declared ? new SourceLocation(file, declared, 1) : new SourceLocation("unknown", 0, 0),
                Attempts = attempts ?? [Attempt()],
            },
        };

    /// <summary>Upstream's <c>runStarted()</c>.</summary>
    public static RunStartedEvent RunStarted(bool ci = false, string[]? targets = null, string projectRoot = "/project", string? model = null, string? judge = null, string[]? agents = null) =>
        new()
        {
            RunId = "run-1",
            ProjectId = "project",
            ProjectRoot = projectRoot,
            ArtifactsRoot = projectRoot + "/.e2e/artifacts",
            Ci = ci,
            Targets = targets ?? ["chromium"],
            Agents = agents,
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
    public static RunFinishedEvent RunFinished(ReportRunStatus status = ReportRunStatus.Passed, int exitCode = 0, string? reportPath = null, string? aiTracePath = null, IReadOnlyDictionary<string, string>? traces = null) =>
        new() { Status = status, ExitCode = exitCode, ReportPath = reportPath, AiTracePath = aiTracePath, Traces = traces };

    /// <summary>
    /// A <see cref="TimeProvider"/> that stands still until it is advanced, for upstream's <c>vi.useFakeTimers</c>. A timer fires as the clock passes
    /// each of its ticks, in order, like vitest's <c>advanceTimersByTime</c>.
    /// </summary>
    public sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        private readonly List<FakeTimer> _timers = [];
        private DateTimeOffset _now = now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan span)
        {
            var target = _now + span;
            while (_timers.Where(timer => timer.Next <= target).MinBy(timer => timer.Next) is { } due)
            {
                _now = due.Next;
                due.Fire();
            }

            _now = target;
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new FakeTimer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        private sealed class FakeTimer(FakeTime clock, TimerCallback callback, object? state) : ITimer
        {
            private TimeSpan _period;

            public DateTimeOffset Next { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                clock._timers.Remove(this);
                _period = period;
                if (dueTime != Timeout.InfiniteTimeSpan)
                {
                    Next = clock._now + dueTime;
                    clock._timers.Add(this);
                }

                return true;
            }

            public void Fire()
            {
                if (_period > TimeSpan.Zero)
                {
                    Next += _period;
                }
                else
                {
                    clock._timers.Remove(this);
                }

                callback(state);
            }

            public void Dispose() => clock._timers.Remove(this);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
