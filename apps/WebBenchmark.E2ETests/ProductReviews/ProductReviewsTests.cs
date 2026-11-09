// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.ProductReviews;

/// <summary>Ported from upstream's <c>apps/web-benchmark/tests/product-reviews.e2e.ts</c>: its one test is outside any <c>describe</c>.</summary>
public sealed class ProductReviewsTests : BenchmarkTest
{
    [Test]
    public async Task The_running_shoe_page_lists_coffee_grinder_reviews_planted_bug()
    {
        await App.OpenAsync("/e/product-reviews");
        await Expect.That(Screen.GetByRole("heading", "Peak Trail Running Shoes")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByText("$139.00 - Sizes 6 to 13 - Ships in 2 days")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByRole("heading", new Regex("Customer reviews"))).ToContainTextAsync("4.7 out of 5");

        var reviews = Screen.GetByTestId("review-list").GetByRole("listitem");
        await Expect.That(reviews).ToHaveCountAsync(3);
        await Expect.That(reviews).ToContainTextAsync(["Marta", "Deon", "Priya"]);
        await Expect.That(reviews).ToContainTextAsync(["espresso", "burrs", "grind settings"]);
        await Expect.That(Screen.GetByTestId("rev-1")).ToContainTextAsync("Grinds beans perfectly evenly");
        await Expect.That(Screen.GetByTestId("rev-1")).Not.ToContainTextAsync("shoe");
    }
}
