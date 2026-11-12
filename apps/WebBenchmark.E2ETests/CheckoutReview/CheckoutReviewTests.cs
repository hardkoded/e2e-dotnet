// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.CheckoutReview;

/// <summary>Ported from upstream's <c>apps/web-benchmark/tests/checkout-review.e2e.ts</c>: its one test is outside any <c>describe</c>.</summary>
public sealed class CheckoutReviewTests : BenchmarkTest
{
    [Test]
    public async Task Shows_a_raw_greeting_token_and_an_undefined_delivery_date_planted_bug_yet_places_the_order()
    {
        await App.OpenAsync("/e/checkout-review");
        await Expect.That(Screen.GetByTestId("checkout-greeting")).ToHaveTextAsync("Thanks, {{firstName}}! Review your order below.");
        await Expect.That(Screen.GetByTestId("delivery-estimate")).ToHaveTextAsync("Delivery: Arrives by undefined");
        await Expect.That(Screen.GetByTestId("order-lines").GetByRole("listitem")).ToContainTextAsync(["Gooseneck Kettle x1", "Single Origin Beans 1kg x2"]);
        await Expect.That(Screen.GetByText("Ship to: 221B Baker Street, London")).ToBeVisibleAsync();

        await Screen.GetByRole("button", "Place order - $123.00").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Order placed successfully");
        await Expect.That(Screen.GetByText(new Regex("Order #10482"))).ToBeVisibleAsync();
    }
}
