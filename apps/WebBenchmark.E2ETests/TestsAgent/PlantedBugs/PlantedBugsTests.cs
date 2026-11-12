// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace WebBenchmark.E2ETests.TestsAgent.PlantedBugs;

/// <summary>
/// Ported from upstream's <c>apps/web-benchmark/tests-agent/planted-bugs.e2e.ts</c> (no <c>describe</c>). Bug-book scenarios: each has one
/// planted product bug, and an <c>assert</c> of the correct behavior must fail on it. A passing judgment here is a missed bug, so the test
/// asserts on the failure. An act that runs into the bug on its way and reports it counts too.
/// </summary>
public sealed class PlantedBugsTests : TestsAgentTest
{
    [Test]
    public async Task Cart_totals_a_judgment_catches_the_stale_order_summary()
    {
        await App.OpenAsync("/e/cart-totals");
        var caught = await CaughtTheBugAsync(async () =>
        {
            await Agent.ActAsync("increase the quantity of the first item in the cart by two");
            await Agent.AssertAsync("the subtotal and the order total in the summary equal the sum of the line totals");
        });
        Assert.That(caught, Is.True);
    }

    [Test]
    public async Task Checkout_review_a_judgment_catches_the_broken_copy()
    {
        await App.OpenAsync("/e/checkout-review");
        // The Blazor page renders its content once the circuit connects, after App.OpenAsync returns, so wait for the greeting before the judge reads the screen.
        await Expect.That(Screen.GetByTestId("checkout-greeting")).ToBeVisibleAsync();
        var caught = await CaughtTheBugAsync(() =>
            Agent.AssertAsync("the greeting addresses the customer by a real first name and the delivery estimate names a date"));
        Assert.That(caught, Is.True);
    }

    [Test]
    public async Task Wishlist_a_judgment_catches_the_lost_saved_items()
    {
        await App.OpenAsync("/e/wishlist");
        var caught = await CaughtTheBugAsync(async () =>
        {
            await Agent.ActAsync("save the first product to the wishlist, then open the wishlist tab");
            await Agent.AssertAsync("the wishlist tab lists the product that was just saved");
        });
        Assert.That(caught, Is.True);
    }

    /// <summary>Whether the flow reported the product misbehaving, as an act verdict or a judgment (upstream's <c>caughtTheBug</c>).</summary>
    private static async Task<bool> CaughtTheBugAsync(Func<Task> flow)
    {
        try
        {
            await flow();
            return false;
        }
        catch (E2EException cause) when (cause.Code is "ASSERTION_FAILED" or "ACTION_FAILED")
        {
            return true;
        }
    }
}
