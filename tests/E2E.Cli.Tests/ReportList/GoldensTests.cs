// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Cli.Report;
using E2E.Internal;
using E2E.Reports;
using static E2E.Cli.Tests.ReportList.ListReporterKit;
using AttemptStatus = E2E.Internal.AttemptStatus;
using Capture = E2E.Cli.Tests.ReportList.ListReporterKit.Capture;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, describe('ListReporter goldens'), against the fixtures of
/// <c>tests/fixtures/list-reporter/</c> (<c>Fixtures/ListReporter/</c> here), run at 80x30 in UTC on a frozen clock. All 5 <c>it</c> are ported.
/// The error location of the failing run is <c>ReportError.Source</c>, where upstream's error carries a stack too.
/// </summary>
public sealed partial class GoldensTests
{
    [GeneratedRegex("\u001b\\[[0-9;?]*[a-zA-Z]")]
    private static partial Regex AnsiPattern();

    /// <summary>A step event for test <paramref name="testId"/> on chromium.</summary>
    private static StepProgressEvent StepOf(string testId, StepProgress progress) => Step(testId, progress);

    private static StepProgressEvent EventOf(string testId, StepEvent stepEvent) =>
        StepOf(testId, new StepProgress { Phase = StepProgressPhase.Event, Api = "agent.act", Event = stepEvent });

    private static StepProgressEvent ActivityOf(string testId, StepActivity activity) =>
        StepOf(testId, new StepProgress { Phase = StepProgressPhase.Activity, Api = "agent.act", Activity = activity });

    private static StepProgressEvent ReplayOf(string testId, bool active) =>
        StepOf(testId, new StepProgress { Phase = StepProgressPhase.Replay, Api = "agent.act", Active = active });

    private static SetupEvent Service(string label, SetupState state, long durationMs = 0) => new(new ServiceSetupStep(label), state, durationMs);

    [Fact]
    public void Prints_a_passing_run_header_with_agents_model_and_CI_a_ready_service_file_blocks_usage_cache_and_the_summary()
    {
        using var terminal = GoldenTerminal();
        var time = new FakeTime(FrozenNow);
        var output = new Capture();
        var reporter = PlainReporter(output, time);
        reporter.Handle(RunStarted(targets: ["chromium", "firefox"], ci: true, model: "openai/gpt-5.6-luna-fast", agents: ["buyer", "admin"]));
        reporter.Handle(Service("service \"compose\"", SetupState.Started));
        reporter.Handle(Service("service \"compose\"", SetupState.Finished, 43_000));
        reporter.Handle(Plan(("tests/checkout.e2e.ts", 2), ("tests/home.e2e.ts", 1)));
        reporter.Handle(TestStarted("pays", "checkout > pays", "chromium", "tests/checkout.e2e.ts"));
        reporter.Handle(StepOf("pays", AgentStep(StepProgressPhase.Start, "pay for the cart")));
        reporter.Handle(StepOf("pays", AgentStep(StepProgressPhase.End, "pay for the cart")));
        time.Advance(TimeSpan.FromMilliseconds(119_500));
        var paid = AgentStepRecord(
            "pay for the cart",
            new CacheInfo { Mode = "missed", Reason = "no-entry", ReplayedActions = 0, TotalActions = 0 },
            model: new StepModelInfo { Provider = "openai", Model = "gpt-5.6-luna-fast", Calls = 3, InputTokens = 12_000, OutputTokens = 400, EstimatedCostUsd = 0.0123 });
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "pays", file: "tests/checkout.e2e.ts", title: ["checkout", "pays"], attempts: [Attempt([paid], durationMs: 4_300)])));
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "refunds", file: "tests/checkout.e2e.ts", title: ["checkout", "refunds"], declarationIndex: 1)));
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "home", file: "tests/home.e2e.ts", title: ["renders"])));
        reporter.Handle(RunFinished(reportPath: "/project/.e2e/report.json", aiTracePath: "/project/.e2e/ai-trace.json"));
        ExpectGolden("list-pass.txt", Stable(string.Join('\n', output.Lines)));
    }

    [Fact]
    public void Prints_a_failing_run_failed_timed_out_interrupted_skipped_and_flaky_results_then_each_failure_with_its_code_frame_and_evidence()
    {
        using var terminal = GoldenTerminal();
        var root = Directory.CreateTempSubdirectory("e2e-list-golden-").FullName;
        try
        {
            var source = Path.Combine(root, "tests", "login.e2e.ts");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, string.Join('\n', "import { test } from '@e2e-dev/web';", "", "await app.open();", "await expect(status).toContainText(\"Welcome\");", "await done();", "", "export {};"));
            var output = new Capture();
            var reporter = PlainReporter(output, new FakeTime(FrozenNow));
            reporter.Handle(RunStarted(projectRoot: root));
            reporter.Handle(Plan(("tests/login.e2e.ts", 5), ("tests/later.e2e.ts", 1), ("tests/retry.e2e.ts", 1)));
            var failed = FailedAttempt("expect.toContainText failed\nexpected \"Welcome\", got \"Sign in\"", new SourceLocation("tests/login.e2e.ts", 4, 22)) with
            {
                Failure = new FailureEvidence { Url = "https://app.test/login", Candidates = ["heading \"Sign in\""], Screen = "attempt-1:artifact:2" },
                Artifacts =
                [
                    new ReportArtifact("attempt-1:artifact:0", "video", "chromium/login/attempt-0/video/video.webm"),
                    new ReportArtifact("attempt-1:artifact:1", "download", "chromium/login/attempt-0/downloads/export.csv"),
                    new ReportArtifact("attempt-1:artifact:2", "log", "chromium/login/attempt-0/screen.txt"),
                ],
            };
            reporter.Handle(Finished(Result(ResultStatus.Passed, id: "opens", file: "tests/login.e2e.ts", title: ["login", "opens"])));
            reporter.Handle(Finished(Result(ResultStatus.Failed, id: "welcomes", file: "tests/login.e2e.ts", title: ["login", "welcomes"], declarationIndex: 1, attempts: [failed])));
            reporter.Handle(Finished(Result(ResultStatus.TimedOut, id: "slow", file: "tests/login.e2e.ts", title: ["login", "slow"], declarationIndex: 2, attempts: [Attempt(status: AttemptStatus.TimedOut, durationMs: 30_000)])));
            reporter.Handle(Finished(Result(ResultStatus.Interrupted, id: "cut", file: "tests/login.e2e.ts", title: ["login", "cut"], declarationIndex: 3, attempts: [Attempt(status: AttemptStatus.Interrupted)])));
            reporter.Handle(Finished(Result(ResultStatus.Skipped, id: "sso", file: "tests/login.e2e.ts", title: ["login", "signs in with SSO"], declarationIndex: 4, skipReason: "no SSO in test")));
            reporter.Handle(Finished(Result(ResultStatus.Skipped, id: "later", file: "tests/later.e2e.ts", title: ["later"], skipReason: "wip feature", attempts: [])));
            var judged = AgentStepRecord("check the cart", model: new StepModelInfo { Provider = "typesafe-ai", Model = "jev", Calls = 2, InputTokens = 900, OutputTokens = 100 });
            reporter.Handle(Finished(Result(ResultStatus.Flaky, id: "retry", file: "tests/retry.e2e.ts", title: ["retries"], attempts: [FailedAttempt("first try"), Attempt([judged], id: "attempt-2", durationMs: 50)])));
            reporter.Handle(RunFinished(ReportRunStatus.Failed, 1, reportPath: Path.Combine(root, ".e2e", "report.json")));
            reporter.Rows([("JUnit", ".e2e/junit.xml")]);
            ExpectGolden("list-fail.txt", Stable(string.Join('\n', output.Lines), root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Prints_a_config_failure_the_run_error_empty_counters_and_no_report()
    {
        using var terminal = GoldenTerminal();
        var output = new Capture();
        var reporter = PlainReporter(output, new FakeTime(FrozenNow));
        reporter.Handle(new RunErrorEvent(new Reports.ReportError { Category = ErrorCategory.Configuration, Code = "INVALID_CONFIG", Message = "unknown config key \"nope\"", Retryable = false, Phase = ErrorPhase.Config }));
        reporter.Handle(RunFinished(ReportRunStatus.Error, 2));
        ExpectGolden("list-error.txt", Stable(string.Join('\n', output.Lines)));
    }

    [Fact]
    public void Prints_an_interrupted_run_the_setup_step_it_cut_each_interrupt_the_failure_limit_and_the_interrupt_wording_on_empty_counters()
    {
        using var terminal = GoldenTerminal();
        var output = new Capture();
        var reporter = PlainReporter(output, new FakeTime(FrozenNow));
        reporter.Handle(RunStarted(model: "gateway/openai/gpt-5.6-luna", agents: ["ux"]));
        reporter.Handle(Service("service \"postgres\"", SetupState.Started));
        reporter.Handle(new RunInterruptedEvent(InterruptMode.Graceful));
        reporter.Handle(new RunInterruptedEvent(InterruptMode.Forced));
        reporter.Handle(new RunStoppedEvent(3, 3));
        reporter.Handle(RunFinished(ReportRunStatus.Interrupted, 130, reportPath: "/project/.e2e/report.json"));
        ExpectGolden("list-interrupted.txt", Stable(string.Join('\n', output.Lines)));
    }

    [Fact]
    public void Paints_the_live_window_through_a_run_setup_counters_finished_and_current_steps_each_activity_reasoning_cache_replay_and_the_blocks()
    {
        using var terminal = GoldenTerminal();
        var time = new FakeTime(FrozenNow);
        List<string> transcript = [];
        List<string> frames = [];
        var output = new FramesCapture(transcript, frames);
        var reporter = PlainReporter(output, time, live: true);
        void Snap(string moment)
        {
            var frame = Regex.Replace(Regex.Replace(frames[^1], "\n+$", string.Empty), "\n{3,}", run => $"\n⋮ {run.Length - 1} blank rows\n");
            transcript.Add($"┄┄┄ frame: {moment} ┄┄┄");
            transcript.AddRange(frame.Split('\n'));
            transcript.Add("┄┄┄");
        }

        reporter.Handle(RunStarted());
        reporter.Handle(new NoticeEvent("chromium", "Downloading missing Playwright browsers (first run): chromium..."));
        Snap("before the plan, no clock");
        reporter.Handle(Service("service \"compose\"", SetupState.Started));
        time.Advance(TimeSpan.FromMilliseconds(900));
        Snap("a setup step in flight");
        reporter.Handle(Service("service \"compose\"", SetupState.Finished, 900));
        reporter.Handle(Plan(("tests/checkout.e2e.ts", 2), ("tests/home.e2e.ts", 1), ("tests/admin.e2e.ts", 1)));
        Snap("the plan");
        reporter.Handle(TestStarted("pays", "pays", "chromium", "tests/checkout.e2e.ts"));
        reporter.Handle(TestStarted("refunds", "refunds", "chromium", "tests/checkout.e2e.ts"));
        reporter.Handle(StepOf("pays", AgentStep(StepProgressPhase.Start, "add to cart")));
        reporter.Handle(StepOf("pays", AgentStep(StepProgressPhase.End, "add to cart")));
        reporter.Handle(StepOf("pays", new StepProgress { Phase = StepProgressPhase.Start, Kind = StepKind.Agent, Api = "agent.act", Label = "pay" }));
        reporter.Handle(ActivityOf("pays", StepActivity.Action));
        time.Advance(TimeSpan.FromMilliseconds(1_000));
        Snap("a finished step, then the current one acting");
        reporter.Handle(EventOf("pays", new StepEvent { Kind = StepEventKind.Engine, Name = "tool:tap", Detail = "tap button \"Pay\"", StartedAt = FrozenNow, DurationMs = 27, Status = StepEventStatus.Passed }));
        reporter.Handle(ActivityOf("pays", StepActivity.Observe));
        Snap("observing after an action");
        reporter.Handle(EventOf("pays", new StepEvent { Kind = StepEventKind.Observation, StartedAt = FrozenNow, DurationMs = 16_000, Status = StepEventStatus.Passed }));
        reporter.Handle(EventOf("pays", new StepEvent { Kind = StepEventKind.Model, StartedAt = FrozenNow, DurationMs = 1_200, Status = StepEventStatus.Passed, Count = 1_100 }));
        Snap("the screen read done, a model turn by token count, and the model thinking again");

        // The reasoning is only on the public event: the port's own step event keeps none.
        var thinking = new Reports.StepEvent
        {
            Kind = Reports.StepEventKind.Model,
            StartedAt = FrozenNow,
            DurationMs = 9_000,
            Status = Reports.StepEventStatus.Passed,
            InputTokens = 6,
            OutputTokens = 127,
            Reasoning = "The modal blocks checkout.\nClosing it first.",
        };
        reporter.Handle(new StepProgressEvent("pays", "default", 0, "chromium", new Reports.StepProgress { Phase = Reports.StepProgressPhase.Event, Api = "agent.act", Event = thinking }));
        Snap("a model turn with its reasoning, then thinking again");
        reporter.Handle(ReplayOf("pays", true));
        reporter.Handle(EventOf("pays", new StepEvent { Kind = StepEventKind.Engine, Name = "tap", Detail = "tap button \"Confirm\"", StartedAt = FrozenNow, DurationMs = 31, Status = StepEventStatus.Passed }));
        Snap("the trace cache replaying");
        reporter.Handle(ReplayOf("pays", false));
        Snap("the cache handing off to the model");
        reporter.Handle(StepOf("pays", new StepProgress { Phase = StepProgressPhase.End, Kind = StepKind.Agent, Api = "agent.act", Label = "pay", Status = StepStatus.Passed, DurationMs = 30_000, ModelCalls = 2 }));
        reporter.Handle(StepOf("refunds", new StepProgress { Phase = StepProgressPhase.Start, Kind = StepKind.Locator, Api = "locator.tap", Label = "Refund" }));
        Snap("a deterministic step, with no wait row");
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "pays", file: "tests/checkout.e2e.ts", title: ["pays"])));
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "refunds", file: "tests/checkout.e2e.ts", title: ["refunds"], declarationIndex: 1)));
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "home", file: "tests/home.e2e.ts", title: ["renders"])));
        reporter.Handle(Finished(Result(ResultStatus.Failed, id: "admin", file: "tests/admin.e2e.ts", title: ["bans"], attempts: [FailedAttempt("ban button missing")])));
        Snap("counters after the results");
        reporter.Handle(RunFinished(ReportRunStatus.Failed, 1, reportPath: "/project/.e2e/report.json"));
        ExpectGolden("list-live-frames.txt", Stable(string.Join('\n', transcript)));
    }

    /// <summary>The live capture of upstream's golden: the written lines go to the transcript, the raw control writes to <c>frames</c>, both with their colors stripped.</summary>
    private sealed class FramesCapture(List<string> transcript, List<string> frames) : IListReporterOutput
    {
        public Action<string>? Raw { get; } = text => frames.Add(AnsiPattern().Replace(text, string.Empty));

        public void Write(string line) => transcript.Add(AnsiPattern().Replace(line, string.Empty));
    }
}
