// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.ExplorePlan.PlanAnswers;

namespace E2E.Tests.ExplorePlan;

public sealed class RepairPlanTests
{
    private static PlanDecision Repair(string answer) => E2E.ExplorePlan.RepairPlan(Parsed(answer));

    [Fact]
    public void Trims_a_step_and_its_title_and_passes_a_finish_through()
    {
        Assert.Equal(new PlanDecision.Step("Cart", "Open the cart."), Repair(Step(title: " Cart ", instruction: " Open the cart. ")));
        Assert.Equal(new PlanDecision.Finish("Checkout works; the cart total is wrong."), Repair(Finish(summary: " Checkout works; the cart total is wrong. ")));
    }

    [Fact]
    public void Uses_a_lone_title_as_the_charter_and_derives_the_heading_from_its_first_clause()
    {
        const string charter = "Open the cart page: add two copies of Dune, then check that the total equals twice the unit price";
        Assert.Equal(new PlanDecision.Step("Open the cart page", charter), Repair(Step(title: charter)));
    }

    [Fact]
    public void Derives_a_heading_for_a_charter_with_no_title_bounded()
    {
        var longCharter = string.Join(' ', Enumerable.Repeat("word", 40)) + " then stop";
        var plan = Assert.IsType<PlanDecision.Step>(Repair(Step(instruction: longCharter)));
        Assert.Equal(longCharter, plan.Instruction);
        Assert.True(plan.Title.Length <= 80);
    }

    [Fact]
    public void Replaces_an_over_long_title_with_a_derived_one()
    {
        Assert.Equal(new PlanDecision.Step("Do the thing", "Do the thing"), Repair(Step(title: new string('x', 300), instruction: "Do the thing")));
    }

    [Fact]
    public void Strips_the_digit_runs_a_provider_pads_a_field_with()
    {
        var padded = "Browse catalog and manage cart nav flow" + string.Concat(Enumerable.Repeat("1234567890", 60));
        Assert.Equal(new PlanDecision.Step("Browse catalog and manage cart nav flow", "Browse catalog and manage cart nav flow"), Repair(Step(title: padded)));
        Assert.Equal(
            new PlanDecision.Step("Sign in", "Open the sign in page and sign in"),
            Repair(Step(title: "Sign in", instruction: "Open the sign in page " + string.Concat(Enumerable.Repeat("0123456789", 3)) + " and sign in")));
        Assert.Equal(new PlanDecision.Finish("All good."), Repair(Finish(summary: "All good." + string.Concat(Enumerable.Repeat("9876543210", 4)))));

        // Numbers a charter can mean are content, not padding: ids, phone numbers, card numbers.
        Assert.Equal(new PlanDecision.Step("Order 1042", "Check order #1042 for $28.00"), Repair(Step(title: "Order 1042", instruction: "Check order #1042 for $28.00")));
        const string tracking = "Confirm tracking number 1234567890123 shows for order 4111111111111111 and phone 5551234567";
        Assert.Equal(new PlanDecision.Step("Tracking", tracking), Repair(Step(title: "Tracking", instruction: tracking)));
    }

    [Fact]
    public void Accepts_a_charter_far_past_the_report_ceiling_and_leaves_its_length_to_the_state()
    {
        var huge = "Open the catalog. " + string.Concat(Enumerable.Repeat("Check every price and stock count carefully. ", 120));
        Assert.True(PlanSchema.SafeParse(Step(title: huge)).Success);
        var plan = Assert.IsType<PlanDecision.Step>(Repair(Step(title: huge)));
        Assert.Equal(huge.Trim(), plan.Instruction);
        Assert.Equal("Open the catalog", plan.Title);
        Assert.True(PlanSchema.SafeParse(Finish(summary: new string('s', 6_000))).Success);
        Assert.False(PlanSchema.SafeParse(Step(title: new string('x', 8_001))).Success);
    }
}
