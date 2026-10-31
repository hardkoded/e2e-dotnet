// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>Upstream: packages/e2e/tests/unit/report-list.test.ts, ListReporter describe('summary').</summary>
public sealed class SummaryTests
{
    [Fact]
    public void Counts_timed_out_results_as_failures_and_interrupted_ones_on_their_own_listing_an_interrupted_test_under_its_file_but_not_among_the_failures()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(Finished(Result(ResultStatus.TimedOut, id: "a")));
        reporter.Handle(Finished(Result(ResultStatus.Interrupted, id: "b", file: "tests/b.e2e.ts")));
        reporter.Handle(RunFinished(ReportRunStatus.Failed, 1, ".e2e/report.json"));
        Assert.Contains(" Test Files  1 failed | 1 interrupted | 0 passed (2)", output.Lines);
        Assert.Contains("      Tests  1 failed | 1 interrupted | 0 passed (2)", output.Lines);
        Assert.Single(output.Lines, line => line.Contains("Failed Tests", StringComparison.Ordinal));

        var interrupted = new Capture();
        var cut = PlainReporter(interrupted);
        cut.Handle(Plan(("tests/a.e2e.ts", 2), ("tests/b.e2e.ts", 1)));
        cut.Handle(Finished(Result(ResultStatus.Passed, id: "a", file: "tests/a.e2e.ts", title: ["done"])));
        cut.Handle(Finished(Result(ResultStatus.Interrupted, id: "b", file: "tests/a.e2e.ts", title: ["cut"])));
        cut.Handle(Finished(Result(ResultStatus.Passed, id: "c", file: "tests/b.e2e.ts")));
        cut.Handle(RunFinished(ReportRunStatus.Interrupted, 130, ".e2e/report.json"));
        Assert.Contains(" ❯ |chromium| tests/a.e2e.ts (2 tests | 1 interrupted) 240ms", interrupted.Lines);
        Assert.Contains("   × cut (interrupted) 120ms", interrupted.Lines);
        Assert.DoesNotContain("Failed Tests", string.Join('\n', interrupted.Lines));
        Assert.Contains(" Test Files  1 interrupted | 1 passed (2)", interrupted.Lines);
        Assert.Contains("      Tests  1 interrupted | 2 passed (3)", interrupted.Lines);
    }

    [Fact]
    public void Starts_the_clock_at_plan_so_a_download_narrated_before_it_is_not_on_it()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero));
        var output = new Capture();
        var reporter = PlainReporter(output, time);
        reporter.Handle(RunStarted());
        reporter.Handle(new NoticeEvent("web", "Downloading missing Playwright browsers (first run): chromium..."));
        time.Advance(TimeSpan.FromSeconds(15));
        var executionStart = time.GetLocalNow();
        reporter.Handle(Plan(("tests/a.e2e.ts", 1)));
        time.Advance(TimeSpan.FromSeconds(2));
        reporter.Handle(Finished(Result(ResultStatus.Passed, file: "tests/a.e2e.ts")));
        reporter.Handle(RunFinished(reportPath: "r.json"));
        Assert.Contains($"   Start at  {Format.FormatClock(executionStart)}", output.Lines);
        Assert.Contains("   Duration  2.00s", output.Lines);
    }

    [Fact]
    public void Counts_a_run_that_never_reached_plan_from_its_launch()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero));
        var output = new Capture();
        var reporter = PlainReporter(output, time);
        var launch = time.GetLocalNow();
        reporter.Handle(RunStarted());
        time.Advance(TimeSpan.FromMilliseconds(1_500));
        reporter.Handle(new RunErrorEvent(new ReportError("NO_TESTS", "no test file matched tests/**/*.e2e.ts", "collection") { Category = "configuration" }));
        reporter.Handle(RunFinished(ReportRunStatus.Error, 2, "r.json"));
        Assert.Contains($"   Start at  {Format.FormatClock(launch)}", output.Lines);
        Assert.Contains("   Duration  1.50s", output.Lines);
    }

    [Fact]
    public void Names_the_models_the_steps_reported_on_the_AI_row_most_calls_first_while_the_header_keeps_the_configuration()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted(model: "gateway/openai/gpt-5.6-luna", judge: "anthropic/claude-sonnet-4.5"));
        reporter.Handle(Plan(("tests/a.e2e.ts", 2)));
        var acted = AgentStep("agent.act", new StepModelInfo { Provider = "typesafe-ai", Model = "jev", Calls = 22, InputTokens = 10_000, OutputTokens = 400 });
        var judged = AgentStep("agent.assert", new StepModelInfo { Provider = "anthropic", Model = "claude-sonnet-4.5", Calls = 1, InputTokens = 2_000, OutputTokens = 0 });
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "test-1", title: ["suite", "acts"], file: "tests/a.e2e.ts", attempts: [Attempt([acted])])));
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "test-2", title: ["suite", "judges"], declarationIndex: 1, file: "tests/a.e2e.ts", attempts: [Attempt([judged])])));
        reporter.Handle(RunFinished(reportPath: "r.json"));
        Assert.Equal("      model gateway/openai/gpt-5.6-luna · judge anthropic/claude-sonnet-4.5", output.Lines[3]);
        Assert.Contains("         AI  12.4k tokens · 23 model calls · typesafe-ai/jev (22 calls) · anthropic/claude-sonnet-4.5 (1 call)", output.Lines);
    }

    [Fact]
    public void Falls_back_to_the_configured_label_never_undefined_when_a_step_reports_calls_without_provenance()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted(model: "openai/gpt-5.6-luna-fast"));
        reporter.Handle(Plan(("tests/a.e2e.ts", 1)));
        var step = AgentStep("agent.act", new StepModelInfo { Calls = 2, InputTokens = 1_000, OutputTokens = 100 });
        reporter.Handle(Finished(Result(ResultStatus.Passed, file: "tests/a.e2e.ts", attempts: [Attempt([step])])));
        reporter.Handle(RunFinished(reportPath: "r.json"));
        Assert.Contains("         AI  1.1k tokens · 2 model calls · openai/gpt-5.6-luna-fast", output.Lines);
        Assert.DoesNotContain(output.Lines, line => line.Contains("undefined", StringComparison.Ordinal));
    }

    [Fact]
    public void Tallies_the_trace_cache_under_the_AI_row_by_step_leaving_zero_counts_out()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted(model: "openai/gpt-5.6-luna-fast"));
        reporter.Handle(Plan(("tests/a.e2e.ts", 1)));
        static StepRecord Step(string id, CacheInfo? cache, int calls) =>
            AgentStep("agent.act", new StepModelInfo { Provider = "openai", Model = "gpt-5.6-luna-fast", Calls = calls, InputTokens = 1_000, OutputTokens = 100 }, id, cache);
        List<StepRecord> steps =
        [
            Step("replayed-1", new CacheInfo { Mode = "self-finalized", ReplayedActions = 3, TotalActions = 3 }, 0),
            Step("replayed-2", new CacheInfo { Mode = "self-finalized", ReplayedActions = 2, TotalActions = 2 }, 0),
            Step("handed-off", new CacheInfo { Mode = "agent-concluded", Reason = "end-mismatch", ReplayedActions = 1, TotalActions = 3 }, 2),
            Step("uncached", null, 3),
        ];
        reporter.Handle(Finished(Result(ResultStatus.Passed, file: "tests/a.e2e.ts", attempts: [Attempt(steps)])));
        reporter.Handle(RunFinished(reportPath: "r.json"));
        var ai = output.Lines.FindIndex(line => line.TrimStart().StartsWith("AI", StringComparison.Ordinal));
        Assert.Equal("      Cache  2 replayed · 1 handed off", output.Lines[ai + 1]);
    }

    [Fact]
    public void Names_the_interrupt_from_run_finished_alone_when_the_host_aborted_without_the_event()
    {
        var output = new Capture();
        PlainReporter(output).Handle(RunFinished(ReportRunStatus.Interrupted, 130));
        Assert.Contains(" Test Files  none started (interrupted)", output.Lines);
        Assert.Contains("      Tests  none executed (interrupted)", output.Lines);
    }

    [Fact]
    public void Keeps_the_counters_of_a_run_interrupted_after_its_plan_arrived()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(Plan(("tests/a.e2e.ts", 3)));
        reporter.Handle(Finished(Result(ResultStatus.Passed, id: "a", file: "tests/a.e2e.ts", title: ["first"])));
        reporter.Handle(new RunInterruptedEvent(InterruptMode.Graceful));
        reporter.Handle(RunFinished(ReportRunStatus.Interrupted, 130));
        Assert.Contains("interrupted: stopping the running test and tearing down (interrupt again to force)", output.Lines);
        Assert.Contains("      Tests  1 passed (3)", output.Lines);
        Assert.DoesNotContain("(interrupted)", string.Join('\n', output.Lines));
    }

    /// <summary>The agent step upstream's <c>step()</c> helpers build: a passed <c>agent.act</c> that reported its model.</summary>
    private static StepRecord AgentStep(string api, StepModelInfo model, string? id = null, CacheInfo? cache = null) =>
        new()
        {
            Id = id ?? api,
            Index = 0,
            Kind = StepKind.Agent,
            Api = api,
            Label = "do it",
            StartedAt = DateTimeOffset.UnixEpoch,
            DurationMs = 10,
            Cache = cache,
            Model = model,
        };
}
