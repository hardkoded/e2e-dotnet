// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using State = E2E.ExploreState;

namespace E2E.Tests.ExploreState;

public sealed class ExploreStateProgressTests
{
    [Fact]
    public void Announces_every_change_to_its_listeners_in_order_with_the_bounded_records()
    {
        var budgets = new ExploreBudgets(4, TimeSpan.FromMinutes(5));
        var state = new State("goal", budgets);
        var seen = new List<ExploreProgress>();
        state.Subscribe(seen.Add);
        state.Start();
        state.Planning(false);
        state.BeginStep("Cart", "a");
        ExploreStateTests.Finding(state, new FindingInput(ReportExploreFindingKind.Issue, 4, "Total shows $0.00", "e", "a", ["x"], Path: "/cart"));
        state.EndStep(ReportExploreStepStatus.Passed, "covered");
        state.Planning(true);
        state.End(ReportExploreEnded.StepLimit, "Checkout is broken.");

        Assert.Equal(
            [
                typeof(ExploreProgress.Started),
                typeof(ExploreProgress.Planning),
                typeof(ExploreProgress.StepStarted),
                typeof(ExploreProgress.FindingReported),
                typeof(ExploreProgress.StepFinished),
                typeof(ExploreProgress.Planning),
                typeof(ExploreProgress.Finished),
            ],
            seen.Select(progress => progress.GetType()));
        Assert.Equal(new ExploreProgress.Started("goal", budgets), seen[0]);
        Assert.Equal(new ExploreProgress.Planning(false), seen[1]);
        Assert.Equal(new ExploreProgress.Planning(true), seen[5]);
        Assert.Equal(new OpenedStep(1, "Cart", "a"), Assert.IsType<ExploreProgress.StepStarted>(seen[2]).Step);
        var finding = Assert.IsType<ExploreProgress.FindingReported>(seen[3]).Finding;
        Assert.Equal(0, finding.Index);
        Assert.Equal(1, finding.Step);
        Assert.Equal("Total shows $0.00", finding.Title);
        Assert.Equal("/cart", finding.Path);
        var step = Assert.IsType<ExploreProgress.StepFinished>(seen[4]).Step;
        Assert.Equal(1, step.Index);
        Assert.Equal(ReportExploreStepStatus.Passed, step.Status);
        Assert.Equal("covered", step.Summary);
        Assert.Equal(new ExploreProgress.Finished(ReportExploreEnded.StepLimit, "Checkout is broken."), seen[6]);
    }

    [Fact]
    public void Announces_the_end_without_a_summary_when_none_was_given()
    {
        var state = new State("goal", new ExploreBudgets(4, TimeSpan.FromMinutes(5)));
        var seen = new List<ExploreProgress>();
        state.Subscribe(seen.Add);
        state.End(ReportExploreEnded.Aborted, "  ");
        Assert.Equal([new ExploreProgress.Finished(ReportExploreEnded.Aborted, null)], seen);
    }
}
