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
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, describe('explore runs'). All 5 <c>it</c> are ported, each driving a
/// <see cref="ListReporter"/> with the stream of run events, as upstream does. The golden is the fixture <c>list-explore.txt</c>.
/// </summary>
public sealed partial class ExploreRunsTests
{
    private const string Goal = "Explore checkout like a first-time buyer";

    private static readonly DateTimeOffset Start = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

    private static readonly ExploreBudgets Budgets = new(4, TimeSpan.FromMilliseconds(300_000));

    private static readonly ReportExploreFinding Finding = new(
        "f-1",
        0,
        1,
        ReportExploreFindingKind.Issue,
        4,
        "Cart total shows $0.00 with two items",
        "The total equals the sum of the two line items",
        "The cart shows \"Total: $0.00\" under two items priced $12.00 and $8.00 each",
        ["Open the catalog", "Add two books to the cart", "Open the cart"],
        Start.AddSeconds(5),
        "/cart",
        "shot-1");

    private static readonly ReportExploreFinding Warning = new(
        "f-2",
        1,
        2,
        ReportExploreFindingKind.Warning,
        1,
        "Footer misspells Receive",
        "Receive",
        "Recieve",
        ["Scroll to the footer"],
        Start.AddSeconds(20),
        "/");

    private static readonly OpenedStep Step1 = new(1, "Cart", "Add two books and open the cart");

    private static readonly OpenedStep Step2 = new(2, "Checkout", "Pay for the cart");

    private static readonly ReportExploreStep Step1Done = new(1, "Cart", Step1.Instruction, ReportExploreStepStatus.Passed, Start.AddSeconds(1), TimeSpan.FromMilliseconds(32_000), "Cart holds two items");

    private static readonly ReportExploreStep Step2Done = new(
        2,
        "Checkout",
        Step2.Instruction,
        ReportExploreStepStatus.Failed,
        Start.AddSeconds(40),
        TimeSpan.FromMilliseconds(91_000),
        "The Pay button stayed disabled with every field filled",
        "STEP_FAILED");

    private const string Assessment = "Checkout cannot be completed. Script the cart total.";

    private static ExploreEvent Explore(ExploreProgress progress) => new(progress);

    private static StepProgressEvent ExploreStep(Reports.StepProgress progress) => new("t1", "default", 0, "web", progress);

    private static StepProgressEvent EngineEvent(string name, string? detail = null) =>
        ExploreStep(new Reports.StepProgress
        {
            Phase = Reports.StepProgressPhase.Event,
            Api = "agent.act",
            Event = new Reports.StepEvent { Kind = Reports.StepEventKind.Engine, Name = name, StartedAt = Start.AddSeconds(4), DurationMs = 40, Status = Reports.StepEventStatus.Passed, Detail = detail },
        });

    private static Reports.StepProgress AgentExtract(Reports.StepProgressPhase phase) =>
        phase == Reports.StepProgressPhase.Start
            ? new Reports.StepProgress { Phase = phase, Kind = Reports.StepKind.Agent, Api = "agent.extract", Label = "Plan the next step" }
            : new Reports.StepProgress { Phase = phase, Kind = Reports.StepKind.Agent, Api = "agent.extract", Label = "Plan the next step", Status = Reports.StepStatus.Passed, DurationMs = 900, ModelCalls = 1 };

    private static Reports.StepProgress AgentAct(Reports.StepProgressPhase phase) =>
        phase == Reports.StepProgressPhase.Start
            ? new Reports.StepProgress { Phase = phase, Kind = Reports.StepKind.Agent, Api = "agent.act", Label = Step1.Instruction }
            : new Reports.StepProgress { Phase = phase, Kind = Reports.StepKind.Agent, Api = "agent.act", Label = Step1.Instruction, Status = Reports.StepStatus.Passed, DurationMs = 32_000, ModelCalls = 6 };

    private static PlanEvent PlanOn(string target) => new(1, [("explore", target, 1)]);

    /// <summary>Upstream's <c>play()</c>: the whole exploration from the run's start to its end, as the run's stream carries it.</summary>
    private static void PlayThrough(global::E2E.Cli.Report.ListReporter reporter)
    {
        reporter.Handle(RunStarted(targets: ["web"]));
        reporter.Handle(PlanOn("web"));
        reporter.Handle(TestStarted("t1", Goal, "web", "explore"));
        reporter.Handle(Explore(new ExploreProgress.Started(Goal, Budgets)));
        reporter.Handle(Explore(new ExploreProgress.Planning(false)));
        reporter.Handle(ExploreStep(AgentExtract(Reports.StepProgressPhase.Start)));
        reporter.Handle(ExploreStep(AgentExtract(Reports.StepProgressPhase.End)));
        reporter.Handle(Explore(new ExploreProgress.StepStarted(Step1)));
        reporter.Handle(ExploreStep(AgentAct(Reports.StepProgressPhase.Start)));
        reporter.Handle(EngineEvent("tap", "tap link \"Catalog\""));
        reporter.Handle(EngineEvent("tap", "tap button \"Add to cart\""));
        reporter.Handle(EngineEvent("tool:report_finding"));
        reporter.Handle(Explore(new ExploreProgress.FindingReported(Finding)));
        reporter.Handle(ExploreStep(AgentAct(Reports.StepProgressPhase.End)));
        reporter.Handle(Explore(new ExploreProgress.StepFinished(Step1Done)));
        reporter.Handle(Explore(new ExploreProgress.Planning(false)));
        reporter.Handle(Explore(new ExploreProgress.StepStarted(Step2)));
        reporter.Handle(Explore(new ExploreProgress.FindingReported(Warning)));
        reporter.Handle(Explore(new ExploreProgress.StepFinished(Step2Done)));
        reporter.Handle(Explore(new ExploreProgress.Finished(ReportExploreEnded.Finished, Assessment)));
        reporter.Handle(Finished(Result(
            ResultStatus.Failed,
            title: [Goal],
            id: "t1",
            file: "explore",
            target: "web",
            attempts:
            [
                Attempt(
                    status: AttemptStatus.Failed,
                    error: new ReportError("ASSERTION_FAILED", "exploration found 1 issue(s): [severity 4] Cart total shows $0.00 with two items"),
                    artifacts: [new ReportArtifact("shot-1", "screenshot", "web/explore-checkout-like-a-first-time-buyer-1ec1fbacae1eaff1/default/attempt-0/finding-1.png")]),
            ])));
        reporter.Handle(RunFinished(ReportRunStatus.Failed, 1, "/project/.e2e/report.json"));
    }

    [Fact]
    public void Without_a_live_window_streams_the_header_each_finding_and_each_step_as_they_happen_then_the_findings_with_their_evidence()
    {
        using var terminal = GoldenTerminal();
        var output = new Capture();
        PlayThrough(PlainReporter(output, new FakeTime(FrozenNow)));
        ExpectGolden("list-explore.txt", Stable(ClockAndDuration(string.Join('\n', output.Lines))));
    }

    [Fact]
    public void Badges_the_exploration_with_the_target_its_test_started_on_not_the_first_configured_one()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted(targets: ["capacity", "mixed"]));
        reporter.Handle(PlanOn("mixed"));
        reporter.Handle(TestStarted("t1", Goal, "mixed", "explore"));
        reporter.Handle(Explore(new ExploreProgress.Started(Goal, Budgets)));

        var header = output.Lines.First(line => line.Contains("Exploring", StringComparison.Ordinal));
        Assert.Contains("|mixed|", header, StringComparison.Ordinal);
        Assert.DoesNotContain("|capacity|", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Badges_the_record_with_the_explored_target_after_its_test_has_finished()
    {
        var output = new LiveCapture();
        var reporter = PlainReporter(output, live: true);
        reporter.Handle(RunStarted(targets: ["capacity", "mixed"]));
        reporter.Handle(PlanOn("mixed"));
        reporter.Handle(TestStarted("t1", Goal, "mixed", "explore"));
        reporter.Handle(Explore(new ExploreProgress.Started(Goal, Budgets)));
        reporter.Handle(Explore(new ExploreProgress.Finished(ReportExploreEnded.Finished, "Nothing to report.")));
        reporter.Handle(Finished(Result(ResultStatus.Passed, title: [Goal], id: "t1", file: "explore", target: "mixed", attempts: [Attempt(status: AttemptStatus.Passed)])));
        reporter.Handle(RunFinished(ReportRunStatus.Passed, 0, "/project/.e2e/report.json"));

        var header = output.Lines.First(line => line.Contains("Explored", StringComparison.Ordinal));
        Assert.Contains("|mixed|", header, StringComparison.Ordinal);
        Assert.DoesNotContain("|capacity|", header, StringComparison.Ordinal);
    }

    [Fact]
    public void With_a_live_window_shows_the_exploration_in_the_window_and_prints_the_record_with_its_steps_once_the_run_is_over()
    {
        var output = new LiveCapture();
        var reporter = PlainReporter(output, live: true);

        // The window's repaint is the last write after each event: the frame is what that write painted.
        string Frame() => StripAnsi(output.Chunks[^1]);
        reporter.Handle(RunStarted(targets: ["web"]));
        reporter.Handle(PlanOn("web"));
        reporter.Handle(TestStarted("t1", Goal, "web", "explore"));
        reporter.Handle(Explore(new ExploreProgress.Started(Goal, Budgets)));
        reporter.Handle(Explore(new ExploreProgress.Planning(false)));
        Assert.Contains("↳ planning step 1 of 4", Frame(), StringComparison.Ordinal);
        Assert.Contains("Steps  0 done of 4 · planning", Frame(), StringComparison.Ordinal);

        // The budget can end the run before the step cap: the planner is then writing the assessment, not a step.
        reporter.Handle(Explore(new ExploreProgress.Planning(true)));
        Assert.Contains("↳ planning the closing assessment", Frame(), StringComparison.Ordinal);
        Assert.Contains("Steps  0 done of 4 · closing", Frame(), StringComparison.Ordinal);
        reporter.Handle(Explore(new ExploreProgress.StepStarted(Step1)));
        reporter.Handle(ExploreStep(AgentAct(Reports.StepProgressPhase.Start)));
        reporter.Handle(ExploreStep(new Reports.StepProgress
        {
            Phase = Reports.StepProgressPhase.Event,
            Api = "agent.act",
            Event = new Reports.StepEvent
            {
                Kind = Reports.StepEventKind.Model,
                StartedAt = Start.AddSeconds(3),
                DurationMs = 2_500,
                Status = Reports.StepEventStatus.Passed,
                Reasoning = "The cart total is stale; adding an item first.",
            },
        }));
        reporter.Handle(EngineEvent("tap", "tap button \"Add to cart\""));
        reporter.Handle(EngineEvent("tool:report_finding"));
        reporter.Handle(Explore(new ExploreProgress.FindingReported(Finding)));
        var frame = Frame();
        Assert.Contains($"Exploring  {Goal}", frame, StringComparison.Ordinal);
        Assert.Contains("↳ 1  Cart (step 1 of 4)", frame, StringComparison.Ordinal);
        Assert.Contains("• Thinking (2.50s) · The cart total is stale; adding an item first.", frame, StringComparison.Ordinal);
        Assert.Contains("› tap button \"Add to cart\" (40ms)", frame, StringComparison.Ordinal);
        Assert.Contains("⚑ high issue  Cart total shows $0.00 with two items (/cart)", frame, StringComparison.Ordinal);
        Assert.DoesNotContain("report_finding", frame, StringComparison.Ordinal);
        Assert.Contains("Findings  1 issue", frame, StringComparison.Ordinal);
        Assert.Contains("Steps  0 done of 4 · step 1 running", frame, StringComparison.Ordinal);
        Assert.DoesNotContain(output.Lines, line => line.Contains("Exploring", StringComparison.Ordinal));
        PlayThrough(reporter);

        Assert.Contains($" × |web| Explored  {Goal}", output.Lines);
        Assert.Contains("   ✓ 1  Cart 32.00s · 2 actions · 1 finding", output.Lines);
        Assert.Contains("   × 2  Checkout 91.00s · 1 finding failed", output.Lines);
        Assert.DoesNotContain("Failed Tests", string.Join('\n', output.Lines), StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_a_failure_that_is_not_the_findings_verdict_such_as_a_run_that_explored_nothing()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted(targets: ["web"]));
        reporter.Handle(PlanOn("web"));
        reporter.Handle(TestStarted("t1", Goal, "web", "explore"));
        reporter.Handle(Explore(new ExploreProgress.Started(Goal, Budgets)));
        reporter.Handle(Explore(new ExploreProgress.Finished(ReportExploreEnded.Aborted)));
        reporter.Handle(Finished(Result(
            ResultStatus.Failed,
            title: [Goal],
            id: "t1",
            file: "explore",
            target: "web",
            attempts:
            [
                Attempt(
                    status: AttemptStatus.Failed,
                    error: new ReportError("AUTOMATION_UNSUPPORTED", "exploration concluded nothing: no step ran and no finding was recorded")),
            ])));
        reporter.Handle(RunFinished(ReportRunStatus.Failed, 1));

        Assert.Contains("   no step ran", output.Lines);
        Assert.Contains("   ended: the run was cut short", output.Lines);
        Assert.Contains("AUTOMATION_UNSUPPORTED: exploration concluded nothing", string.Join('\n', output.Lines), StringComparison.Ordinal);
        Assert.Contains("   Findings  none", output.Lines);
        Assert.Contains("      Steps  0 of 4 · the run was cut short", output.Lines);
    }

    /// <summary>The clock rows are the only ones that differ between runs.</summary>
    private static string ClockAndDuration(string text) => Duration().Replace(Clock().Replace(text, "Start at  10:00:00"), "Duration  0ms");

    [GeneratedRegex(@"Start at  \d\d:\d\d:\d\d")]
    private static partial Regex Clock();

    [GeneratedRegex(@"Duration  [\d.]+m?s")]
    private static partial Regex Duration();
}
