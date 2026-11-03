// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, ListReporter describe('file blocks'). Not ported: the three <c>it</c> that run a live
/// window (<c>with a live window, prints one line per file ...</c>, <c>lists every test under the run's only file</c>, <c>sums durations across
/// attempts and marks flaky results</c>), and <c>clips the first error line to the terminal width; the failure section keeps it whole</c>,
/// because <c>Format.TerminalColumns</c> reads the console and the port has no way to set the width.
/// </summary>
public sealed class FileBlocksTests
{
    [Fact]
    public void Without_a_live_window_prints_each_test_as_it_finishes_and_the_file_line_once_the_file_completes()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/auth.e2e.ts", 2)));
        output.Lines.Clear();
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "a", file: "tests/auth.e2e.ts", title: ["auth", "signs in"], attempts: [Attempt() with { DurationMs = 80 }])));
        Assert.Equal(["   ✓ |chromium| tests/auth.e2e.ts > auth > signs in 80ms"], output.Lines);
        reporter.Handle(Finished(Result(ResultStatus.Failed, id: "b", file: "tests/auth.e2e.ts", title: ["auth", "signs out"], attempts: [FailedAttempt("still signed in")])));
        Assert.Equal(
            [
                "   ✓ |chromium| tests/auth.e2e.ts > auth > signs in 80ms",
                "   × |chromium| tests/auth.e2e.ts > auth > signs out 120ms",
                "     → still signed in",
                " ❯ |chromium| tests/auth.e2e.ts (2 tests | 1 failed) 200ms",
            ],
            output.Lines);
    }

    [Fact]
    public void Tags_a_test_that_ran_as_an_agent_other_than_default_so_a_persona_sweep_lists_once_per_agent()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/checkout.e2e.ts", 3)));
        output.Lines.Clear();
        foreach (var agent in new[] { "default", "buyer", "admin" })
        {
            reporter.Handle(TestStarted("t1", "checkout > pays", "chromium", "tests/checkout.e2e.ts", agent));
        }

        // Steps route by test id and agent, so the buyer's step stays with the buyer's line.
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.Start, "pay"), "buyer"));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.End, "pay"), "buyer"));
        foreach (var agent in new[] { "default", "buyer", "admin" })
        {
            reporter.Handle(Finished(Result(ResultStatus.Passed, id: "t1", agent: agent, file: "tests/checkout.e2e.ts", title: ["checkout", "pays"], attempts: [Attempt() with { DurationMs = 80 }])));
        }

        Assert.Equal(
            [
                "   ✓ checkout > pays [buyer] > agent.act \"pay\" 4.20s · 3 model calls",
                "   ✓ |chromium| tests/checkout.e2e.ts > checkout > pays 80ms",
                "   ✓ |chromium| tests/checkout.e2e.ts > checkout > pays [buyer] 80ms",
                "   ✓ |chromium| tests/checkout.e2e.ts > checkout > pays [admin] 80ms",
                " ✓ |chromium| tests/checkout.e2e.ts (3 tests) 240ms",
            ],
            output.Lines);
    }

    [Fact]
    public void Suppresses_deselected_skipped_results_entirely()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(Finished(Result(ResultStatus.Skipped, selected: false)));
        reporter.Handle(RunFinished(reportPath: "r.json"));
        var text = string.Join('\n', output.Lines);
        Assert.DoesNotContain("tests/case.e2e.ts", text);
        Assert.Contains("no tests executed", text);
    }

    [Fact]
    public void Prints_unfinished_files_at_the_end_of_an_interrupted_run()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(Plan(("tests/a.e2e.ts", 3)));
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "a", file: "tests/a.e2e.ts", title: ["first"])));
        Assert.Equal(["   ✓ |chromium| tests/a.e2e.ts > first 120ms"], output.Lines);
        reporter.Handle(RunFinished(ReportRunStatus.Failed, 1));
        Assert.Equal(" ✓ |chromium| tests/a.e2e.ts (1 test) 120ms", output.Lines[1]);
    }

    [Fact]
    public void Groups_the_same_file_per_target_colored_by_target_order()
    {
        var chunks = new List<string>();
        var reporter = new ListReporter(new Recorder(chunks), new ListReporterOptions { Colors = true });
        reporter.Handle(RunStarted(targets: ["chromium", "firefox"]));
        reporter.Handle(new PlanEvent(2, [("tests/a.e2e.ts", "chromium", 1), ("tests/a.e2e.ts", "firefox", 1)]));
        reporter.Handle(Finished(Result(ResultStatus.Passed, target: "firefox", file: "tests/a.e2e.ts")));
        reporter.Handle(Finished(Result(ResultStatus.Passed, target: "chromium", file: "tests/a.e2e.ts")));
        var blocks = chunks.Where(line => line.Contains("tests/a.e2e.ts", StringComparison.Ordinal) && !line.Contains(" > ", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, blocks.Count);
        Assert.Contains(" firefox ", blocks[0], StringComparison.Ordinal);
        Assert.Contains(" chromium ", blocks[1], StringComparison.Ordinal);
        Assert.Contains("\u001b[103m", blocks[1], StringComparison.Ordinal);
        Assert.Contains("\u001b[106m", blocks[0], StringComparison.Ordinal);
    }

    private sealed class Recorder(List<string> lines) : IListReporterOutput
    {
        public void Write(string line) => lines.Add(line);
    }
}
