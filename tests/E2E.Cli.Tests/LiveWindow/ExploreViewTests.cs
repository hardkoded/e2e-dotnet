// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;

namespace E2E.Cli.Tests.LiveWindow;

/// <summary>
/// Upstream: packages/e2e/tests/unit/live-window.test.ts, describe('ExploreView'). All 3 <c>it</c> are ported. The terminal size that upstream's
/// <c>withTerminalSize</c> sets on <c>process.stdout</c> is <c>Format.WithTerminalSize</c> (.NET-only).
/// </summary>
public sealed class ExploreViewTests
{
    private static readonly Colors Plain = new(false);

    private static readonly ExploreBudgets Budgets = new(4, TimeSpan.FromMilliseconds(300_000));

    [Fact]
    public void Clips_a_CJK_goal_in_the_header_to_the_columns_after_the_badge_and_verb()
    {
        var view = new ExploreView(Plain, new ExploreProgress.Started(new string('日', 50), Budgets));
        var header = view.Header("|web|", ExploreVerb.Exploring, 60);
        Assert.Equal($" ❯ |web| Exploring  {new string('日', 19)}…", header);
        Assert.Equal(59, Format.VisibleWidth(header));
        Assert.Equal($" ❯ |web| Exploring  {new string('日', 50)}", view.Header("|web|", ExploreVerb.Exploring));
    }

    [Fact]
    public void Counts_a_finding_on_the_step_it_was_reported_in_also_when_it_arrives_after_that_step_ended()
    {
        var view = new ExploreView(Plain, new ExploreProgress.Started("checkout", Budgets));
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00.000Z", System.Globalization.CultureInfo.InvariantCulture);
        OpenedStep Step(int index) => new(index, $"Step {index}", "go");
        ReportExploreStep Finished(int index) =>
            new(
                index,
                $"Step {index}",
                "go",
                ReportExploreStepStatus.Exhausted,
                start,
                TimeSpan.FromMilliseconds(1_000),
                null
            );
        var finding = new ReportExploreFinding(
            "f",
            0,
            1,
            ReportExploreFindingKind.Issue,
            4,
            "Total shows $0.00",
            "a total",
            "$0.00",
            ["open the cart"],
            start.AddSeconds(1)
        );
        view.Progress(new ExploreProgress.StepStarted(Step(1)));
        view.Progress(new ExploreProgress.StepFinished(Finished(1)));
        view.Progress(new ExploreProgress.StepStarted(Step(2)));
        view.Progress(new ExploreProgress.FindingReported(finding));
        view.Progress(new ExploreProgress.StepFinished(Finished(2)));
        Assert.Equal(0, view.LastStep?.Findings);
        Assert.Matches("Step 1 .*1 finding", string.Join("\n", view.Record("|web|", true)));
    }

    [Fact]
    public void Clips_a_failed_steps_CJK_summary_to_the_terminal_width_in_columns()
    {
        using var terminal = Format.WithTerminalSize(rows: 40, columns: 60);
        var view = new ExploreView(Plain, new ExploreProgress.Started("checkout", Budgets));
        var step = new ReportExploreStep(
            1,
            "Cart",
            "Open the cart",
            ReportExploreStepStatus.Failed,
            DateTimeOffset.Parse("2026-01-01T00:00:00.000Z", System.Globalization.CultureInfo.InvariantCulture),
            TimeSpan.FromMilliseconds(32_000),
            new string('日', 40)
        );
        var summary = view.StepLines(new ExploreView.FinishedStep(step, 0, 0))[1];
        Assert.Equal($"        {new string('日', 24)}…", summary);
        Assert.Equal(57, Format.VisibleWidth(summary));
    }
}
