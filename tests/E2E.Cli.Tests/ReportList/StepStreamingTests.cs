// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, ListReporter describe('step streaming'). Not ported: the two <c>it</c> that run a live
/// window (<c>with a live window, holds finished agent steps and nests them under their test in the block</c>) or clip a wide-glyph label by
/// column (<c>clips a step label to its 72-column budget by column without a live window, whatever the terminal width</c>): the port has no live
/// window, and <c>Format.FitColumns</c> counts a wide glyph as one column.
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
