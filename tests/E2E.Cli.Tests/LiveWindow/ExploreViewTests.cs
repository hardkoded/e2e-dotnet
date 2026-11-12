// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;

namespace E2E.Cli.Tests.LiveWindow;

/// <summary>
/// Upstream: packages/e2e/tests/unit/live-window.test.ts, describe('ExploreView'). All 2 <c>it</c> are ported. The terminal size that upstream's
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
    public void Clips_a_failed_steps_CJK_summary_to_the_terminal_width_in_columns()
    {
        using var terminal = Format.WithTerminalSize(rows: 40, columns: 60);
        var view = new ExploreView(Plain, new ExploreProgress.Started("checkout", Budgets));
        var step = new ReportExploreStep(1, "Cart", "Open the cart", ReportExploreStepStatus.Failed, DateTimeOffset.Parse("2026-01-01T00:00:00.000Z", System.Globalization.CultureInfo.InvariantCulture), TimeSpan.FromMilliseconds(32_000), new string('日', 40));
        var summary = view.StepLines(new ExploreView.FinishedStep(step, 0, 0))[1];
        Assert.Equal($"        {new string('日', 24)}…", summary);
        Assert.Equal(57, Format.VisibleWidth(summary));
    }
}
