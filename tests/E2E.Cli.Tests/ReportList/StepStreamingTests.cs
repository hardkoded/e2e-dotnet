// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, ListReporter describe('step streaming'). All 5 <c>it</c> are ported. The terminal width of
/// the wide-glyph one is set with <c>Format.WithTerminalSize</c> (.NET-only), since a console's size cannot be set from a test.
/// </summary>
public sealed class StepStreamingTests
{
    [Fact]
    public void Without_a_live_window_prints_finished_agent_steps_at_once_with_their_test_then_the_test_and_its_file()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(Plan(("tests/flow.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "checkout", "chromium", "tests/flow.e2e.ts"));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.Start, "add to cart")));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.End, "add to cart")));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.Start, "pay")));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.End, "pay", StepStatus.Failed)));
        reporter.Handle(Finished(Result(ResultStatus.Failed, id: "t1", file: "tests/flow.e2e.ts", title: ["checkout"], attempts: [FailedAttempt("pay step failed")])));
        Assert.Equal(
            [
                "   ✓ checkout > agent.act \"add to cart\" 4.20s · 3 model calls",
                "   × checkout > agent.act \"pay\" 4.20s · 3 model calls failed",
                "   × |chromium| tests/flow.e2e.ts > checkout 120ms",
                "     → pay step failed",
                " ❯ |chromium| tests/flow.e2e.ts (1 test | 1 failed) 120ms",
            ],
            output.Lines);
    }

    [Fact]
    public void With_a_live_window_holds_finished_agent_steps_and_nests_them_under_their_test_in_the_block()
    {
        var output = new Capture { Raw = _ => { } };
        var reporter = PlainReporter(output, live: true);
        reporter.Handle(Plan(("tests/flow.e2e.ts", 2)));
        reporter.Handle(TestStarted("t1", "checkout", "chromium", "tests/flow.e2e.ts"));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.Start, "add to cart")));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.End, "add to cart")));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.Start, "pay")));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.End, "pay", StepStatus.Failed)));
        Assert.Empty(output.Lines);
        reporter.Handle(Finished(Result(ResultStatus.Failed, id: "t1", file: "tests/flow.e2e.ts", title: ["checkout"], declarationIndex: 0, attempts: [FailedAttempt("pay step failed")])));
        reporter.Handle(TestStarted("t2", "refund", "chromium", "tests/flow.e2e.ts"));
        reporter.Handle(Step("t2", AgentStep(StepProgressPhase.Start, "refund order")));
        reporter.Handle(Step("t2", AgentStep(StepProgressPhase.End, "refund order")));
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "t2", file: "tests/flow.e2e.ts", title: ["refund"], declarationIndex: 1)));
        Assert.Equal(
            [
                " ❯ |chromium| tests/flow.e2e.ts (2 tests | 1 failed) 240ms",
                "   × checkout 120ms",
                "     → pay step failed",
                "     ✓ agent.act \"add to cart\" 4.20s · 3 model calls",
                "     × agent.act \"pay\" 4.20s · 3 model calls failed",
                "   ✓ refund 120ms",
                "     ✓ agent.act \"refund order\" 4.20s · 3 model calls",
            ],
            output.Lines);
    }

    [Fact]
    public void Prefixes_finished_agent_steps_with_their_test_when_tests_run_in_parallel()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(Plan(("tests/flow.e2e.ts", 2)));
        reporter.Handle(TestStarted("t1", "checkout", "chromium", "tests/flow.e2e.ts"));
        reporter.Handle(TestStarted("t2", "refund", "chromium", "tests/flow.e2e.ts"));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.Start, "add to cart")));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.End, "add to cart")));
        Assert.Equal(["   ✓ checkout > agent.act \"add to cart\" 4.20s · 3 model calls"], output.Lines);
    }

    [Fact]
    public void Clips_a_step_label_to_its_72_column_budget_by_column_without_a_live_window_whatever_the_terminal_width()
    {
        using var terminal = Format.WithTerminalSize(columns: 60);
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(Plan(("tests/flow.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "checkout", "chromium", "tests/flow.e2e.ts"));
        var label = new string('日', 40);
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.Start, label)));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.End, label)));
        var clipped = new string('日', 35) + "…";
        Assert.Equal($"   ✓ checkout > agent.act \"{clipped}\" 4.20s · 3 model calls", output.Lines[^1]);
        Assert.Equal(71, Format.VisibleWidth(clipped));
    }

    [Fact]
    public void Keeps_deterministic_steps_out_of_the_permanent_log()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(TestStarted("t1", "checkout", "chromium"));
        reporter.Handle(Step("t1", new StepProgress { Phase = StepProgressPhase.Start, Kind = StepKind.Locator, Api = "locator.tap", Label = "button" }));
        reporter.Handle(Step("t1", new StepProgress { Phase = StepProgressPhase.End, Kind = StepKind.Locator, Api = "locator.tap", Label = "button", Status = StepStatus.Passed, DurationMs = 5, ModelCalls = 0 }));
        Assert.Empty(output.Lines);
    }
}
