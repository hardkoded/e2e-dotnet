// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Plan = E2E.ExplorePlan;
using State = E2E.ExploreState;

namespace E2E.Tests.ExplorePlan;

public sealed class PlanInstructionTests
{
    private static State Fresh() => new("Explore checkout like a first-time buyer", new ExploreBudgets(6, TimeSpan.FromMinutes(10)));

    [Fact]
    public void Opens_with_the_goal_an_empty_record_and_the_two_decisions()
    {
        var text = Plan.PlanInstruction(Fresh(), new PlanRequest(false, TimeSpan.FromMilliseconds(540_000), TimeSpan.FromMilliseconds(60_000)));
        Assert.Contains("Goal: Explore checkout like a first-time buyer", text, StringComparison.Ordinal);
        Assert.Contains("0 of 6 steps used, about 9 minute(s) left", text, StringComparison.Ordinal);
        Assert.Contains("(none yet: this is the first step)", text, StringComparison.Ordinal);
        Assert.Contains("\"decision\": \"step\"", text, StringComparison.Ordinal);
        Assert.Contains("\"decision\": \"finish\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Lists_the_steps_with_their_outcome_and_the_findings_so_far()
    {
        var state = Fresh();
        state.BeginStep("Cart", "open the cart");
        state.EndStep(ReportExploreStepStatus.Passed, "Cart opened");
        state.BeginStep("Pay", "pay for the cart");
        state.EndStep(ReportExploreStepStatus.Exhausted, "ran out of actions", "STEP_BUDGET_EXHAUSTED");
        state.AddFinding(new FindingInput(ReportExploreFindingKind.Issue, 4, "Total is $0.00", "a total", "$0.00", ["open the cart"]));
        var text = Plan.PlanInstruction(state, new PlanRequest(false, TimeSpan.FromMilliseconds(300_000), TimeSpan.FromMilliseconds(60_000)));
        Assert.Contains("1. [passed] Cart — Cart opened", text, StringComparison.Ordinal);
        Assert.Contains("2. [ended at its limit] Pay — ran out of actions", text, StringComparison.Ordinal);
        Assert.Contains("- [issue, severity 4] Total is $0.00", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Asks_only_for_the_assessment_when_the_run_must_finish()
    {
        var text = Plan.PlanInstruction(Fresh(), new PlanRequest(true, TimeSpan.FromMilliseconds(100_000), TimeSpan.FromMilliseconds(60_000), Reason: "the step limit of 6 is reached"));
        Assert.Contains("The run must end now: the step limit of 6 is reached.", text, StringComparison.Ordinal);
        Assert.Contains("\"decision\": \"finish\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"decision\": \"step\"", text, StringComparison.Ordinal);
    }

    // Upstream also checks the text for a run that restored a saved session. The port has no saved sessions.
    [Fact]
    public void Plans_the_sign_in_with_the_accounts_listed_when_no_session_was_restored()
    {
        var text = Plan.PlanInstruction(Fresh(), new PlanRequest(false, TimeSpan.FromMilliseconds(300_000), TimeSpan.FromMilliseconds(60_000)));
        Assert.DoesNotContain("starts signed in", text, StringComparison.Ordinal);
        Assert.Contains("sign in with the accounts listed above", text, StringComparison.Ordinal);
    }
}
