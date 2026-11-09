// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.CartTotals;

/// <summary>Ported from <c>describe('cart totals')</c> in upstream's <c>apps/web-benchmark/tests/cart-totals.e2e.ts</c>.</summary>
public sealed class CartTotalsTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheCartTotals() => await App.OpenAsync("/e/cart-totals");

    [Test]
    public async Task Increasing_a_quantity_updates_the_line_total_but_the_order_summary_stays_stale_planted_bug()
    {
        await Expect.That(Screen.GetByTestId("summary-subtotal")).ToHaveTextAsync("$49.00");
        await Expect.That(Screen.GetByTestId("summary-total")).ToHaveTextAsync("$54.00");

        var tee = LineFor("Organic Cotton Tee");
        await tee.GetByRole("button", "+").TapAsync();
        await tee.GetByRole("button", "+").TapAsync();
        await Expect.That(Screen.GetByTestId("quantity-tee")).ToHaveTextAsync("3");
        await Expect.That(Screen.GetByTestId("line-total-tee")).ToHaveTextAsync("$60.00");

        // Correct math would be $89.00 and $94.00; the summary never recomputes.
        await Expect.That(Screen.GetByTestId("summary-subtotal")).ToHaveTextAsync("$49.00");
        await Expect.That(Screen.GetByTestId("summary-shipping")).ToHaveTextAsync("$5.00");
        await Expect.That(Screen.GetByTestId("summary-total")).ToHaveTextAsync("$54.00");
    }

    [Test]
    public async Task A_quantity_never_drops_below_one()
    {
        var cap = LineFor("Corduroy Cap");
        await cap.GetByRole("button", "-").TapAsync();
        await Expect.That(Screen.GetByTestId("quantity-cap")).ToHaveTextAsync("1");
        await Expect.That(Screen.GetByTestId("line-total-cap")).ToHaveTextAsync("$29.00");
    }

    /// <summary>Upstream's <c>lineFor</c>: the cart row for one product, found by the name it shows.</summary>
    private Locator LineFor(string name) => Screen.GetByRole("listitem").Filter(name);
}
