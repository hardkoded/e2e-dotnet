// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Cli.Report;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, ListReporter describe('live window') &gt; describe('test.step') (tester-army/e2e#2312).
/// All 8 <c>it</c> are ported.
/// </summary>
public sealed partial class TestStepTests
{
    private static global::E2E.Cli.Report.ListReporter Live(LiveCapture output) => PlainReporter(output, live: true);

    /// <summary>Feeds step progress of attempt <c>a1</c> of a test; <c>parent</c> is the index of the <c>test.step</c> around the step.</summary>
    private static Action<int, int?, StepProgress> Feed(
        global::E2E.Cli.Report.ListReporter reporter,
        string testId = "t1",
        int attemptIndex = 0,
        string attemptId = "a1"
    ) =>
        (stepIndex, parent, progress) =>
            reporter.Handle(
                Step(
                    testId,
                    progress with
                    {
                        Identity = new StepProgressIdentity(
                            attemptId,
                            attemptIndex,
                            $"{attemptId}:{stepIndex}",
                            stepIndex,
                            null,
                            parent is { } at ? $"{attemptId}:{at}" : null
                        ),
                    }
                )
            );

    private static StepProgress Started(StepKind kind, string api, string label) =>
        new()
        {
            Phase = StepProgressPhase.Start,
            Kind = kind,
            Api = api,
            Label = label,
        };

    private static StepProgress Ended(
        StepKind kind,
        string api,
        string label,
        StepStatus status = StepStatus.Passed,
        int modelCalls = 0
    ) =>
        new()
        {
            Phase = StepProgressPhase.End,
            Kind = kind,
            Api = api,
            Label = label,
            Status = status,
            DurationMs = 1_200,
            ModelCalls = modelCalls,
        };

    private static StepProgress GroupStart(string label) => Started(StepKind.Step, "test.step", label);

    private static StepProgress GroupEnd(string label, StepStatus status = StepStatus.Passed) =>
        Ended(StepKind.Step, "test.step", label, status);

    private static StepProgress ActStart() => Started(StepKind.Agent, "agent.act", "pay");

    private static StepProgress ActEnd(StepStatus status = StepStatus.Passed, int modelCalls = 0) =>
        Ended(StepKind.Agent, "agent.act", "pay", status, modelCalls);

    private static List<string> Rows(string chunk, string glyphs) =>
        [
            .. StripAnsi(chunk)
                .Split('\n')
                .Where(row => row.IndexOfAny(glyphs.ToCharArray()) >= 0)
                .Select(row => Regex.Replace(row, "^ {7}", "")),
        ];

    private sealed class RawOutput(List<string> lines) : IListReporterOutput
    {
        public void Write(string line) => lines.Add(line);
    }

    [Fact]
    public void Lists_a_running_test_step_where_it_started_the_steps_inside_it_under_it_then_prints_them_nested()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "pays", "chromium"));
        var step = Feed(reporter);
        // Two side by side: each one's steps stay under it however their ends interleave.
        step(0, null, GroupStart("Log in"));
        step(1, null, GroupStart("Check out"));
        step(2, 1, ActStart());
        step(2, 1, ActEnd(modelCalls: 2));
        step(3, 0, GroupStart("Type the password"));
        step(4, 3, Started(StepKind.Locator, "locator.fill", "Password"));
        step(4, 3, Ended(StepKind.Locator, "locator.fill", "Password"));
        step(3, 0, GroupEnd("Type the password"));
        Assert.Equal(
            ["◌ Log in", "  ✓ Type the password 1.20s", "◌ Check out", "  ✓ agent.act \"pay\" 1.20s · 2 model calls"],
            Rows(output.Chunks[^1], "◌✓×")
        );
        step(1, null, GroupEnd("Check out"));
        step(0, null, GroupEnd("Log in", StepStatus.Failed));
        Assert.Equal(
            [
                "× Log in 1.20s failed",
                "  ✓ Type the password 1.20s",
                "✓ Check out 1.20s",
                "  ✓ agent.act \"pay\" 1.20s · 2 model calls",
            ],
            Rows(output.Chunks[^1], "◌✓×")
        );

        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "t1", title: ["pays"])));
        var block = output
            .Lines.Skip(output.Lines.FindIndex(line => line.Contains("pays", StringComparison.Ordinal)) + 1)
            .Where(line => line.IndexOfAny(['×', '✓']) >= 0)
            .ToList();
        // The passed `Type the password` had no agent step in it: it is not kept.
        Assert.Equal(
            [
                "     × Log in 1.20s failed",
                "     ✓ Check out 1.20s",
                "       ✓ agent.act \"pay\" 1.20s · 2 model calls",
            ],
            block
        );
    }

    [Fact]
    public void Draws_an_open_test_step_once_and_keeps_the_call_running_beside_it_as_the_current_step()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "pays", "chromium"));
        var step = Feed(reporter);
        step(0, null, ActStart());
        step(1, null, GroupStart("Log in"));
        var shown = StripAnsi(output.Chunks[^1]);
        Assert.Single(Regex.Matches(shown, "Log in"));
        Assert.Contains("agent.act \"pay\"", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("test.step", shown, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_a_retrys_steps_under_their_test_step_when_a_step_of_the_attempt_before_ends_late()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "pays", "chromium"));
        var before = Feed(reporter, attemptIndex: 0, attemptId: "a0");
        var retry = Feed(reporter, attemptIndex: 1, attemptId: "a1");
        before(0, null, Started(StepKind.Agent, "agent.act", "side"));
        retry(0, null, GroupStart("Log in"));
        before(0, null, Ended(StepKind.Agent, "agent.act", "side", StepStatus.Cancelled));
        retry(1, 0, ActStart());
        retry(1, 0, ActEnd());
        retry(0, null, GroupEnd("Log in"));
        Assert.Equal(
            ["× agent.act \"side\" 1.20s cancelled", "✓ Log in 1.20s", "  ✓ agent.act \"pay\" 1.20s"],
            Rows(output.Chunks[^1], "✓×")
        );
    }

    [Fact]
    public void Keeps_a_test_step_of_the_attempt_before_under_the_one_around_it_when_it_ends_after_a_retry_began()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "pays", "chromium"));
        var before = Feed(reporter, attemptIndex: 0, attemptId: "a0");
        var retry = Feed(reporter, attemptIndex: 1, attemptId: "a1");
        before(0, null, GroupStart("Outer"));
        before(1, 0, GroupStart("Inner"));
        retry(0, null, GroupStart("Outer"));
        before(1, 0, GroupEnd("Inner", StepStatus.Cancelled));
        before(0, null, GroupEnd("Outer", StepStatus.Cancelled));
        retry(0, null, GroupEnd("Outer"));
        Assert.Equal(
            ["× Outer 1.20s cancelled", "  × Inner 1.20s cancelled", "✓ Outer 1.20s"],
            Rows(output.Chunks[^1], "◌✓×")
        );
    }

    [Fact]
    public void Lists_a_test_step_whose_end_never_came_above_the_steps_that_ran_in_it()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "pays", "chromium"));
        var step = Feed(reporter);
        step(0, null, GroupStart("Log in"));
        step(1, 0, ActStart());
        step(1, 0, ActEnd());
        reporter.Handle(Finished(Result(ResultStatus.Failed, id: "t1", title: ["pays"])));
        var block = output
            .Lines.Skip(output.Lines.FindIndex(line => line.Contains("pays", StringComparison.Ordinal)) + 1)
            .Where(line => line.IndexOfAny(['◌', '×', '✓']) >= 0)
            .ToList();
        Assert.Equal(["     ◌ Log in", "       ✓ agent.act \"pay\" 1.20s"], block.Take(2));
    }

    [Fact]
    public void Never_lets_control_characters_of_a_test_step_title_through_in_the_name_of_a_step_printed_inside_it()
    {
        var lines = new List<string>();
        var reporter = PlainReporter(new RawOutput(lines));
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "pays", "chromium"));
        var step = Feed(reporter);
        step(0, null, GroupStart($"Check\u001b[2J out\nsecond {new string('x', 300)}"));
        step(1, 0, ActStart());
        step(1, 0, ActEnd());
        var line = lines.First(printed => printed.Contains("agent.act", StringComparison.Ordinal));
        Assert.False(
            line.Contains('\u001b', StringComparison.Ordinal) || line.Contains('\n', StringComparison.Ordinal)
        );
        Assert.True(line.Length < 200);
        Assert.Contains("pays > Check", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Prints_an_agent_step_as_it_ends_named_after_its_test_and_the_test_step_calls_around_it_without_a_live_window()
    {
        var lines = new List<string>();
        var reporter = PlainReporter(new RawOutput(lines));
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        reporter.Handle(TestStarted("t1", "pays", "chromium"));
        var step = Feed(reporter);
        step(0, null, GroupStart("Check out"));
        step(1, 0, ActStart());
        step(1, 0, ActEnd(modelCalls: 2));
        step(2, 0, Started(StepKind.Locator, "locator.tap", "Pay"));
        step(2, 0, Ended(StepKind.Locator, "locator.tap", "Pay"));
        step(0, null, GroupEnd("Check out"));
        Assert.Equal(
            ["   ✓ pays > Check out > agent.act \"pay\" 1.20s · 2 model calls"],
            lines.Where(line => line.Contains('✓', StringComparison.Ordinal))
        );
    }

    [Fact]
    public void Keeps_a_green_file_of_tests_that_only_name_plain_calls_on_its_one_line()
    {
        var output = new LiveCapture();
        var reporter = Live(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/a.e2e.ts", 1), ("tests/b.e2e.ts", 1)));
        foreach (var (id, file) in new[] { ("t1", "tests/a.e2e.ts"), ("t2", "tests/b.e2e.ts") })
        {
            reporter.Handle(TestStarted(id, "pays", "chromium", file));
            var step = Feed(reporter, id);
            step(0, null, GroupStart("Log in"));
            step(1, 0, Started(StepKind.Locator, "locator.tap", "Pay"));
            step(1, 0, Ended(StepKind.Locator, "locator.tap", "Pay"));
            step(0, null, GroupEnd("Log in"));
            reporter.Handle(Finished(Result(ResultStatus.Passed, id: id, title: ["pays"], file: file)));
        }

        Assert.DoesNotContain(output.Lines, line =>
            line.Contains("Log in", StringComparison.Ordinal) || line.Contains("pays", StringComparison.Ordinal)
        );
        Assert.Equal(2, output.Lines.Count(line => line.Contains(".e2e.ts", StringComparison.Ordinal)));
    }
}
