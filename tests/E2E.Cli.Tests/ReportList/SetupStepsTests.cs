// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>Upstream: packages/e2e/tests/unit/report-list.test.ts, ListReporter describe('setup steps').</summary>
public sealed class SetupStepsTests
{
    [Fact]
    public void Prints_each_process_once_it_is_ready_and_splits_startup_out_of_the_duration()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted());
        reporter.Handle(new SetupEvent(new ServiceSetupStep("service \"compose\""), SetupState.Started));
        reporter.Handle(new SetupEvent(new ServiceSetupStep("service \"compose\""), SetupState.Finished, 41_200));
        var app = new AppSetupStep("target \"chromium\" command", "http://127.0.0.1:4310/");
        reporter.Handle(new SetupEvent(app, SetupState.Started));
        reporter.Handle(new SetupEvent(app, SetupState.Finished, 1_800, SetupOutcome.Reused));
        reporter.Handle(RunFinished(reportPath: "r.json"));
        Assert.Contains(" ✓ service \"compose\" ready 41.20s", output.Lines);
        Assert.Contains(" ✓ target \"chromium\" command at http://127.0.0.1:4310/ reused 1.80s", output.Lines);
        Assert.Matches(@"^   Duration  \S+ \(startup 43\.00s\)\z", output.Lines.Single(line => line.Contains("Duration", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(true, "timed out")]
    [InlineData(false, "failed")]
    public void Prints_a_filter_step_once_it_narrated_never_checks_off_one_that_failed_and_names_the_one_an_interrupt_cuts(bool timedOut, string word)
    {
        var outcome = timedOut ? SetupOutcome.TimedOut : SetupOutcome.Failed;
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted());
        var silent = new FilterSetupStep("silent");
        reporter.Handle(new SetupEvent(silent, SetupState.Started));
        reporter.Handle(new SetupEvent(silent, SetupState.Finished, 300, outcome));
        var jev = new FilterSetupStep("jev");
        reporter.Handle(new SetupEvent(jev, SetupState.Started));
        reporter.Handle(new NoticeEvent("filter", "jev: asking Jev about 4 tests in tests/todos.e2e.ts"));
        reporter.Handle(new SetupEvent(jev, SetupState.Finished, 1_200));
        var areas = new FilterSetupStep("areas");
        reporter.Handle(new SetupEvent(areas, SetupState.Started));
        reporter.Handle(new NoticeEvent("filter", "areas: asking the model about 3 tests"));
        reporter.Handle(new SetupEvent(areas, SetupState.Finished, 302, outcome));
        reporter.Handle(new SetupEvent(new FilterSetupStep("slow"), SetupState.Started));
        reporter.Handle(new RunInterruptedEvent(InterruptMode.Graceful));
        reporter.Handle(RunFinished(ReportRunStatus.Interrupted, 130));
        Assert.Equal(
            [
                "ℹ jev: asking Jev about 4 tests in tests/todos.e2e.ts",
                " ✓ filter \"jev\" done 1.20s",
                "ℹ areas: asking the model about 3 tests",
                $" × filter \"areas\" {word} 302ms",
                "interrupted while running filter \"slow\": tearing down (interrupt again to force)",
            ],
            output.Lines.Where(line => System.Text.RegularExpressions.Regex.IsMatch(line, "jev|areas|silent|slow")));

        var chunks = new Raw();
        var colored = new ListReporter(chunks, new ListReporterOptions { Colors = true });
        colored.Handle(RunStarted());
        colored.Handle(new SetupEvent(areas, SetupState.Started));
        colored.Handle(new NoticeEvent("filter", "areas: asking the model about 3 tests"));
        colored.Handle(new SetupEvent(areas, SetupState.Finished, 302, outcome));
        Assert.Equal($" \u001b[33m×\u001b[39m filter \"areas\" \u001b[33m{word} 302ms\u001b[39m", chunks.Lines[^1]);
    }

    [Fact]
    public void Prints_a_provisioning_step_only_when_it_narrated_and_never_the_collection()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted());
        reporter.Handle(new SetupEvent(new CollectSetupStep(), SetupState.Started));
        reporter.Handle(new SetupEvent(new CollectSetupStep(), SetupState.Finished, 310));
        var prepare = new PrepareSetupStep("web", "playwright");
        reporter.Handle(new SetupEvent(prepare, SetupState.Started));
        reporter.Handle(new SetupEvent(prepare, SetupState.Finished, 2));
        reporter.Handle(new SetupEvent(prepare, SetupState.Started));
        reporter.Handle(new NoticeEvent("web", "Downloading missing Playwright browsers (first run): chromium..."));
        reporter.Handle(new SetupEvent(prepare, SetupState.Finished, 13_200));
        reporter.Handle(RunFinished(reportPath: "r.json"));
        Assert.DoesNotContain(output.Lines, line => line.Contains("collected", StringComparison.Ordinal));
        Assert.Equal([" ✓ playwright engine for target \"web\" prepared 13.20s"], output.Lines.Where(line => line.Contains("prepared", StringComparison.Ordinal)));

        // Provisioning is off the clock: no startup split for it.
        Assert.DoesNotContain("startup", output.Lines.Single(line => line.Contains("Duration", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    /// <summary>Upstream's raw <c>chunks</c> sink: the lines a reporter wrote, colors kept.</summary>
    private sealed class Raw : IListReporterOutput
    {
        public List<string> Lines { get; } = [];

        public void Write(string line) => Lines.Add(line);
    }
}
