// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Time.Testing;
using State = E2E.ExploreState;

namespace E2E.Tests.ExploreState;

public sealed class ExploreStateTests
{
    internal static ReportExploreFinding Finding(State state, FindingInput? input = null) =>
        state.AddFinding(input ?? new FindingInput(
            ReportExploreFindingKind.Issue,
            4,
            "Total shows $0.00",
            "The total reflects the cart",
            "Total: $0.00",
            ["Add two items", "Open the cart"]));

    [Fact]
    public void Records_steps_in_order_with_their_outcome_and_timing()
    {
        var clock = new FakeTimeProvider();
        var state = new State("Explore checkout", new ExploreBudgets(4, TimeSpan.FromMinutes(5)), clock);
        Assert.Equal(1, state.BeginStep("Cart", "Add two items and open the cart").Index);
        clock.Advance(TimeSpan.FromMilliseconds(2_500));
        var step = state.EndStep(ReportExploreStepStatus.Passed, "Cart opened with both items");
        Assert.Equal(1, step.Index);
        Assert.Equal("Cart", step.Title);
        Assert.Equal(ReportExploreStepStatus.Passed, step.Status);
        Assert.Equal(TimeSpan.FromMilliseconds(2_500), step.Duration);
        Assert.Equal("Cart opened with both items", step.Summary);
        Assert.Equal(2, state.BeginStep("Checkout", "Pay for the cart").Index);
        state.EndStep(ReportExploreStepStatus.Failed, "agent.act failed: the pay button did nothing", "ACTION_FAILED");
        Assert.Equal([ReportExploreStepStatus.Passed, ReportExploreStepStatus.Failed], state.Steps.Select(entry => entry.Status));
        Assert.Equal("ACTION_FAILED", state.Steps[1].ErrorCode);
    }

    [Fact]
    public void Refuses_to_open_a_second_step_while_one_is_open_and_to_close_none()
    {
        var state = new State("goal", new ExploreBudgets(2, TimeSpan.FromMinutes(5)));
        Assert.Matches("no exploration step is open", Assert.Throws<InvalidOperationException>(() => state.EndStep(ReportExploreStepStatus.Passed)).Message);
        state.BeginStep("One", "do one");
        Assert.Matches("step 1 is still open", Assert.Throws<InvalidOperationException>(() => state.BeginStep("Two", "do two")).Message);
    }

    [Fact]
    public void Attributes_findings_to_the_step_in_progress_and_keeps_them_ordered()
    {
        var state = new State("goal", new ExploreBudgets(2, TimeSpan.FromMinutes(5)));
        var early = Finding(state, new FindingInput(ReportExploreFindingKind.Issue, 4, "Reported before any step", "a", "b", ["x"]));
        Assert.Null(early.Step);
        state.BeginStep("Cart", "open the cart");
        var inStep = Finding(state, new FindingInput(ReportExploreFindingKind.Issue, 4, "Total shows $0.00", "a", "b", ["x"], ArtifactId: "screenshots/001-finding-1.png"));
        Assert.Equal(1, inStep.Index);
        Assert.Equal(1, inStep.Step);
        Assert.Equal(ReportExploreFindingKind.Issue, inStep.Kind);
        Assert.Equal(4, inStep.Severity);
        Assert.Equal("screenshots/001-finding-1.png", inStep.ArtifactId);
        Assert.Equal(2, state.Issues.Count);
    }

    [Fact]
    public void Opens_a_step_as_the_report_will_record_it_so_the_charter_the_agent_runs_is_the_bounded_one()
    {
        var state = new State("goal", new ExploreBudgets(2, TimeSpan.FromMinutes(5)));
        var opened = state.BeginStep("  " + new string('x', 400) + "  ", new string('y', 5_000));
        Assert.Equal(1, opened.Index);
        Assert.Equal(200, opened.Title.Length);
        Assert.Equal(2_000, opened.Instruction.Length);
        var step = state.EndStep(ReportExploreStepStatus.Passed);
        Assert.Equal(opened.Title, step.Title);
        Assert.Equal(opened.Instruction, step.Instruction);
    }

    [Fact]
    public void Counts_failed_and_blocked_steps_in_a_row_skipping_steps_that_ended_at_their_limit()
    {
        var state = new State("goal", new ExploreBudgets(8, TimeSpan.FromMinutes(5)));
        void Record(ReportExploreStepStatus status)
        {
            state.BeginStep(status.ToString(), "step " + status);
            state.EndStep(status);
        }

        Record(ReportExploreStepStatus.Failed);
        Record(ReportExploreStepStatus.Passed);
        Assert.Equal(0, state.ConsecutiveFailures());
        Record(ReportExploreStepStatus.Failed);
        Record(ReportExploreStepStatus.Exhausted);
        Record(ReportExploreStepStatus.Blocked);
        Assert.Equal(2, state.ConsecutiveFailures());
        Record(ReportExploreStepStatus.Failed);
        Assert.Equal(3, state.ConsecutiveFailures());
    }

    [Fact]
    public void Does_not_count_a_failed_step_that_recorded_a_finding()
    {
        var state = new State("goal", new ExploreBudgets(8, TimeSpan.FromMinutes(5)));
        state.BeginStep("Orders", "inspect orders");
        Finding(state);
        state.EndStep(ReportExploreStepStatus.Failed, "agent.act failed: count mismatch reported");
        state.BeginStep("Account", "save the profile");
        state.EndStep(ReportExploreStepStatus.Failed);
        Assert.Equal(1, state.ConsecutiveFailures());
    }

    // Upstream also validates the snapshot against the report's JSON schema, which the port does not have.
    [Fact]
    public void Snapshots_an_explore_block_with_bounded_text()
    {
        var state = new State("goal", new ExploreBudgets(2, TimeSpan.FromMinutes(5)));
        state.BeginStep(new string('x', 400), new string('y', 5_000));
        state.EndStep(ReportExploreStepStatus.Passed, new string('z', 9_000));
        Finding(state, new FindingInput(ReportExploreFindingKind.Issue, 4, new string('t', 300), "e", "a", [.. Enumerable.Repeat(new string('r', 900), 30)]));
        state.End(ReportExploreEnded.Finished, new string('s', 9_000));
        var snapshot = state.Snapshot();
        Assert.Equal(200, snapshot.Steps[0].Title.Length);
        Assert.Equal(2_000, snapshot.Steps[0].Instruction.Length);
        Assert.Equal(4_000, snapshot.Steps[0].Summary!.Length);
        Assert.Equal(200, snapshot.Findings[0].Title.Length);
        Assert.Equal(20, snapshot.Findings[0].Reproduction.Count);
        Assert.All(snapshot.Findings[0].Reproduction, entry => Assert.Equal(500, entry.Length));
        Assert.Equal(4_000, snapshot.Summary!.Length);
        Assert.Equal(ReportExploreEnded.Finished, snapshot.Ended);
    }

    [Fact]
    public void Is_aborted_with_no_assessment_until_ended_and_drops_a_blank_assessment()
    {
        var state = new State("goal", new ExploreBudgets(2, TimeSpan.FromMinutes(5)));
        Assert.Equal(ReportExploreEnded.Aborted, state.Ended);
        Assert.Null(state.Summary);
        state.End(ReportExploreEnded.Time, "   ");
        Assert.Equal(ReportExploreEnded.Time, state.Ended);
        Assert.Null(state.Snapshot().Summary);
    }

    [Fact]
    public void Clip_trims_and_marks_truncation_counting_code_points_so_no_surrogate_pair_is_split()
    {
        Assert.Equal("hello", State.Clip("  hello  ", 10));
        Assert.Equal("abc…", State.Clip("abcdef", 4));
        Assert.Equal("😀😀…", State.Clip("😀😀😀😀", 3));
        Assert.Equal("😀😀", State.Clip("😀😀", 2));
    }
}
