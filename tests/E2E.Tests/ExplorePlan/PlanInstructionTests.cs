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

    [Fact]
    public void Says_the_run_starts_signed_in_from_a_restored_session_and_plans_no_sign_in()
    {
        var signedOut = Plan.PlanInstruction(Fresh(), new PlanRequest(false, TimeSpan.FromMilliseconds(300_000), TimeSpan.FromMilliseconds(60_000)));
        Assert.DoesNotContain("starts signed in", signedOut, StringComparison.Ordinal);
        Assert.Contains("sign in with the accounts listed above", signedOut, StringComparison.Ordinal);
        var text = Plan.PlanInstruction(Fresh(), new PlanRequest(false, TimeSpan.FromMilliseconds(300_000), TimeSpan.FromMilliseconds(60_000), Session: "admin"));
        Assert.Contains("The app starts signed in: the run restored the session \"admin\" a setup test saved.", text, StringComparison.Ordinal);
        Assert.Contains("work as the signed-in user rather than signing in again", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sign in with the accounts listed above", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_summaries_of_the_latest_eight_steps_only_so_a_long_run_stays_within_the_model_input()
    {
        var state = new State("Explore checkout", new ExploreBudgets(100, TimeSpan.FromMilliseconds(7_200_000)));
        for (var index = 1; index <= 100; index++)
        {
            state.BeginStep("Area " + index, "Look around");
            state.EndStep(ReportExploreStepStatus.Passed, "summary-" + index + " " + new string('x', 4_000));
        }

        var text = Plan.PlanInstruction(state, new PlanRequest(false, TimeSpan.FromMilliseconds(600_000), TimeSpan.FromMilliseconds(60_000)));
        Assert.Contains("1. [passed] Area 1\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("summary-92 ", text, StringComparison.Ordinal);
        Assert.Contains("summary-93 ", text, StringComparison.Ordinal);
        Assert.Contains("summary-100 ", text, StringComparison.Ordinal);
        Assert.True(text.Length < 40_000);
    }

    [Fact]
    public void Offers_the_other_account_lens_only_with_several_accounts_and_no_restored_session()
    {
        PlanAccount[] accounts = [new("ada", "ada@example.test"), new("bob", "bob@example.test")];
        string PlanFor(IReadOnlyList<PlanAccount> list, string? session = null) =>
            Plan.PlanInstruction(Fresh(), new PlanRequest(false, TimeSpan.FromMilliseconds(300_000), TimeSpan.FromMilliseconds(60_000), list, Session: session));
        Assert.Contains("another account or role", PlanFor(accounts), StringComparison.Ordinal);
        Assert.DoesNotContain("another account or role", PlanFor(accounts[..1]), StringComparison.Ordinal);
        Assert.DoesNotContain("another account or role", PlanFor(accounts, "admin"), StringComparison.Ordinal);
    }
}
