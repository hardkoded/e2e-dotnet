// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Cli.Report;
using E2E.Reports;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, ListReporter describe('live window'). All 15 <c>it</c> are ported. The terminal size that
/// upstream's <c>withTerminalSize</c> sets on <c>process.stdout</c> is <c>Format.WithTerminalSize</c> (.NET-only), since a console's size cannot be set
/// from a test. The reporter's repaint timer comes from a <see cref="TimeProvider"/> whose timers fire only as the test advances the clock, as vitest's fake timers do.
/// </summary>
public sealed partial class LiveWindowTests
{
    private static global::E2E.Cli.Report.ListReporter Live(LiveCapture output) => PlainReporter(output, live: true);

    private static string Frame(string chunk) => StripAnsi(chunk);

    private static int Painted(string chunk) => chunk.Split('\n').Length - 1;

    private static StepProgressEvent ModelTurn(string testId, long count = 100) =>
        Step(testId, new StepProgress { Phase = StepProgressPhase.Event, Api = "agent.act", Event = new StepEvent { Kind = StepEventKind.Model, StartedAt = DateTimeOffset.UnixEpoch, DurationMs = 10, Count = count } });

    [Fact]
    public void Shows_a_running_file_and_test_on_start_and_erases_the_window_before_a_result_prints()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "signs in", "chromium"));
        var window = output.Chunks[^1];
        Assert.Contains(" ❯ |chromium| tests/case.e2e.ts 0/1", Frame(window), StringComparison.Ordinal);
        Assert.Contains("└── signs in", Frame(window), StringComparison.Ordinal);
        Assert.Contains("Test Files  0 passed (1)", Frame(window), StringComparison.Ordinal);
        reporter.Handle(Finished(Result(ResultStatus.Passed, title: ["signs in"], id: "t1")));

        // The window above the result line is erased before the block prints.
        Assert.Contains(output.Chunks, chunk => chunk.Contains("\u001b[0J", StringComparison.Ordinal));
        Assert.DoesNotContain("└──", output.Chunks[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Clips_a_finished_step_label_so_the_row_keeps_its_tail_at_the_terminal_width()
    {
        using var terminal = Format.WithTerminalSize(rows: 40, columns: 80);
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/flow.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "checkout", "chromium", "tests/flow.e2e.ts"));
        const string label = "add two todos named \"Buy milk\" and \"Walk the dog\", then mark the first one as done";
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.Start, label)));
        reporter.Handle(Step("t1", AgentStep(StepProgressPhase.End, label)));
        var row = Frame(output.Chunks[^1]).Split('\n').First(line => line.Contains("agent.act", StringComparison.Ordinal));
        Assert.True(row.Length <= 78);
        Assert.Matches(@"^ {7}✓ agent\.act ""add two todos .*…"" 4\.20s · 3 model calls$", row);
    }

    [Fact]
    public void Clamps_a_wide_glyph_AI_row_to_the_window_by_column_and_prints_it_whole_in_the_final_summary()
    {
        using var terminal = Format.WithTerminalSize(rows: 40, columns: 60);
        var output = new LiveCapture();
        var reporter = Live(output);

        // The header's configured label stands in on the row until the steps' own models do (#428).
        reporter.Handle(RunStarted(model: "custom/日本語エージェント"));
        reporter.Handle(Plan(("tests/a.e2e.ts", 2)));
        static StepRecord WithModel(string id, string provider, string model, int calls) =>
            AgentStepRecord("do it", model: new StepModelInfo { Provider = provider, Model = model, Calls = calls, InputTokens = 1_000, OutputTokens = 100 });
        reporter.Handle(TestStarted("t1", "acts", "chromium", "tests/a.e2e.ts"));
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "t1", title: ["suite", "acts"], file: "tests/a.e2e.ts", attempts: [Attempt([WithModel("s1", "custom", "日本語エージェント", 2)])])));
        reporter.Handle(TestStarted("t2", "judges", "chromium", "tests/a.e2e.ts"));
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "t2", title: ["suite", "judges"], declarationIndex: 1, file: "tests/a.e2e.ts", attempts: [Attempt([WithModel("s2", "typesafe-ai", "jev", 1)])])));
        var frame = output.Chunks[^1];
        var row = Frame(frame).Split('\n').First(line => line.TrimStart().StartsWith("AI ", StringComparison.Ordinal));
        Assert.Matches(@"^ {9}AI {2}2\.2k tokens · 3 model calls · .*…$", row);

        // Content stops at the width less WIDTH_MARGIN, then the ellipsis: one column under the terminal, never on it.
        Assert.True(Format.VisibleWidth(row) <= 59);
        Assert.True(Format.VisibleWidth(row) >= 57);
        Assert.DoesNotContain("custom/日本語エージェント", row, StringComparison.Ordinal);
        reporter.Handle(RunFinished(reportPath: "r.json"));
        var printed = output.Lines.First(line => line.TrimStart().StartsWith("AI ", StringComparison.Ordinal));
        Assert.Contains("2.2k tokens · 3 model calls · custom/日本語エージェント", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void Reserves_a_fixed_running_area_so_the_summary_stays_put_as_calls_come_and_go()
    {
        using var terminal = Format.WithTerminalSize(rows: 40, columns: 100);
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/flow.e2e.ts", 1)));
        int SummaryRow(string chunk) => Array.FindIndex(Frame(chunk).Split('\n'), line => line.StartsWith(" Test Files", StringComparison.Ordinal));
        var before = SummaryRow(output.Chunks[^1]);
        reporter.Handle(TestStarted("t1", "checkout", "chromium", "tests/flow.e2e.ts"));
        reporter.Handle(Step("t1", new StepProgress { Phase = StepProgressPhase.Start, Kind = StepKind.Agent, Api = "agent.act", Label = "pay" }));
        for (var i = 0; i < 4; i++)
        {
            reporter.Handle(ModelTurn("t1"));
        }

        var during = SummaryRow(output.Chunks[^1]);
        reporter.Handle(Step("t1", new StepProgress { Phase = StepProgressPhase.End, Kind = StepKind.Agent, Api = "agent.act", Label = "pay", Status = StepStatus.Passed, DurationMs = 40, ModelCalls = 4 }));
        var after = SummaryRow(output.Chunks[^1]);
        Assert.True(before > 5);
        Assert.Equal(before, during);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Prints_a_notice_permanently_above_the_live_window()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(new NoticeEvent("web", "Downloading missing Playwright browsers (first run): chromium..."));
        Assert.Contains("ℹ Downloading missing Playwright browsers (first run): chromium...", output.Lines);

        // The window is erased before the notice prints and repainted after it,
        // so a first-run download narrates in scrollback instead of under the block.
        var eraseIndex = output.Chunks.FindIndex(chunk => EraseSequence().IsMatch(chunk));
        var noticeIndex = output.Chunks.FindIndex(chunk => chunk.Contains("Downloading", StringComparison.Ordinal));
        Assert.True(eraseIndex > -1);
        Assert.True(eraseIndex < noticeIndex);
        Assert.Contains("Tests", output.Chunks[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Never_writes_control_sequences_when_live_rendering_is_off()
    {
        var output = new LiveCapture();
        var reporter = PlainReporter(output, live: false);
        reporter.Handle(RunStarted());
        reporter.Handle(TestStarted("t1", "signs in", "chromium"));
        reporter.Handle(Finished(Result(ResultStatus.Passed)));
        reporter.Handle(RunFinished(reportPath: "r.json"));
        Assert.DoesNotContain("\u001b[", string.Concat(output.Chunks), StringComparison.Ordinal);
    }

    [Fact]
    public void Stays_silent_for_output_sinks_without_a_raw_channel()
    {
        var output = new ListReporterKit.Capture();
        var reporter = new global::E2E.Cli.Report.ListReporter(output, new ListReporterOptions { Live = true, Colors = false });
        reporter.Handle(TestStarted("t1", "signs in", "chromium"));
        Assert.Empty(output.Lines);
    }

    [Fact]
    public void Prints_a_tests_console_output_above_the_window_under_one_heading_per_source()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(TestStarted("t1", "signs in", "chromium"));
        var t1 = new OutputPair("t1", "default", 0);
        reporter.Handle(new OutputEvent("chromium", t1, OutputStream.Stdout, "hello\n"));
        reporter.Handle(new OutputEvent("chromium", t1, OutputStream.Stdout, "two\nlines\n"));
        reporter.Handle(new OutputEvent("chromium", t1, OutputStream.Stderr, "oops\n"));
        reporter.Handle(new OutputEvent("chromium", t1, OutputStream.Stdout, "again\n"));
        reporter.Handle(new OutputEvent("chromium", null, OutputStream.Stdout, "no pair\n"));
        Assert.Equal(
            [
                "stdout | |chromium| tests/case.e2e.ts > signs in",
                "hello",
                "two",
                "lines",
                "stderr | |chromium| tests/case.e2e.ts > signs in",
                "oops",
                "stdout | |chromium| tests/case.e2e.ts > signs in",
                "again",
                "stdout | |chromium|",
                "no pair",
            ],
            output.Lines.Skip(output.Lines.IndexOf("stdout | |chromium| tests/case.e2e.ts > signs in")));
    }

    [Fact]
    public void Joins_a_line_written_in_pieces_and_prints_an_unfinished_one_when_the_tests_result_arrives()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "signs in", "chromium"));
        void Write(string text) => reporter.Handle(new OutputEvent("chromium", new OutputPair("t1", "default", 0), OutputStream.Stdout, text));
        Write("progress ");
        Write("50%\nnext");
        Assert.Equal(["progress 50%"], output.Lines.Where(line => line.StartsWith("progress", StringComparison.Ordinal) || line.StartsWith("next", StringComparison.Ordinal)));
        reporter.Handle(Finished(Result(ResultStatus.Passed, title: ["signs in"], id: "t1")));
        var from = output.Lines.IndexOf("next");
        Assert.True(from > -1);

        // The fragment prints before the test's own line does.
        Assert.Contains(output.Lines.Skip(from), line => line.Contains("signs in", StringComparison.Ordinal));
    }

    [Fact]
    public void Brings_the_output_heading_back_after_another_line_printed_in_between()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(TestStarted("t1", "signs in", "chromium"));
        var t1 = new OutputPair("t1", "default", 0);
        reporter.Handle(new OutputEvent("chromium", t1, OutputStream.Stdout, "one\n"));
        reporter.Handle(new NoticeEvent("chromium", "between"));
        reporter.Handle(new OutputEvent("chromium", t1, OutputStream.Stdout, "two\n"));
        Assert.Equal(2, output.Lines.Count(line => line.StartsWith("stdout |", StringComparison.Ordinal)));
        Assert.Equal("two", output.Lines[^1]);
    }

    [Fact]
    public void Prints_what_the_runner_process_printed_above_the_window_under_a_runner_heading()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.ProcessOutput(OutputStream.Stdout, "from the ");
        reporter.ProcessOutput(OutputStream.Stdout, "config\n");
        reporter.ProcessOutput(OutputStream.Stderr, "a warning\n");
        reporter.Handle(new OutputEvent("chromium", null, OutputStream.Stdout, "from a worker\n"));
        reporter.ProcessOutput(OutputStream.Stdout, "unfinished");
        reporter.Handle(RunFinished(reportPath: "r.json"));
        var at = output.Lines.IndexOf("stdout | runner");
        Assert.Equal(
            [
                "stdout | runner",
                "from the config",
                "stderr | runner",
                "a warning",
                "stdout | |chromium|",
                "from a worker",
                "stdout | runner",
                "unfinished",
            ],
            output.Lines.Skip(at).Take(8));
    }

    [Fact]
    public void Pads_the_block_to_the_bottom_of_a_tall_terminal_and_shrinks_it_as_the_log_grows()
    {
        using var terminal = Format.WithTerminalSize(rows: 60, columns: 120);
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 2)));
        reporter.Handle(TestStarted("t1", "signs in", "chromium"));

        // The header took the rows above; the block takes every row under
        // it but the cursor's, so the summary sits at the bottom of the screen.
        var above = output.Lines.Count;
        Assert.Equal(60 - 1 - above, Painted(output.Chunks[^1]));
        Assert.Matches(@"Duration {2}\d+m?s\n\n$", Frame(output.Chunks[^1]));

        // A permanent notice above the block takes one row from it: no scroll.
        reporter.Handle(new NoticeEvent("chromium", "one more line"));
        Assert.Equal(60 - 1 - above - 1, Painted(output.Chunks[^1]));
    }

    [Fact]
    public void Stops_shrinking_at_the_rows_the_running_area_needs_once_the_log_has_grown_down_to_it()
    {
        using var terminal = Format.WithTerminalSize(rows: 30, columns: 120);
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 2)));
        reporter.Handle(TestStarted("t1", "signs in", "chromium"));
        for (var i = 0; i < 40; i++)
        {
            reporter.Handle(new NoticeEvent("chromium", $"line {i}"));
        }

        var rows = Painted(output.Chunks[^1]);

        // Fourteen running rows plus the frame and the summary, no more.
        Assert.True(rows >= 14 + 3 + 2);
        Assert.True(rows < 29);
        Assert.Contains("└── signs in", output.Chunks[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Counts_a_wrapped_permanent_line_by_its_rows()
    {
        using var terminal = Format.WithTerminalSize(rows: 40, columns: 40);
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(TestStarted("t1", "signs in", "chromium"));
        var before = Painted(output.Chunks[^1]);
        reporter.Handle(new NoticeEvent("chromium", new string('x', 70)));
        Assert.Contains(new string('x', 70), output.Lines[^1], StringComparison.Ordinal);

        // Seventy-two visible characters on a forty-column terminal wrap onto two rows.
        Assert.Equal(before - 2, Painted(output.Chunks[^1]));
    }

    [Fact]
    public void Keeps_the_window_within_the_terminal_height_and_folds_the_tests_that_do_not_fit()
    {
        using var terminal = Format.WithTerminalSize(rows: 8, columns: 60);
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/a.e2e.ts", 4)));
        foreach (var id in new[] { "a", "b", "c", "d" })
        {
            reporter.Handle(TestStarted(id, $"test {id}", "chromium", "tests/a.e2e.ts"));
            reporter.Handle(Step(id, new StepProgress { Phase = StepProgressPhase.Start, Kind = StepKind.Agent, Api = "agent.act", Label = $"step {id}" }));
            for (var i = 0; i < 5; i++)
            {
                reporter.Handle(ModelTurn(id));
            }
        }

        Assert.True(output.Chunks.Max(Painted) <= 7);
        Assert.True(output.Chunks.Max(chunk => EraseRows().Match(chunk) is { Success: true } erase ? int.Parse(erase.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0) <= 7);
        Assert.Contains("more running", output.Chunks[^1], StringComparison.Ordinal);
    }

    [GeneratedRegex("\u001b\\[\\d+A\u001b\\[0J$")]
    private static partial Regex EraseSequence();

    [GeneratedRegex("\u001b\\[(\\d+)A")]
    private static partial Regex EraseRows();
}
