// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.PriceSorting;

/// <summary>Ported from <c>describe('price sorting')</c> in upstream's <c>apps/web-benchmark/tests/price-sorting.e2e.ts</c>.</summary>
public sealed class PriceSortingTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenThePriceSorting() => await App.OpenAsync("/e/price-sorting");

    [Test]
    public async Task Opens_in_the_featured_order_with_sale_prices_struck_through()
    {
        var rows = Screen.GetByTestId("product-list").GetByRole("listitem");
        await Expect.That(rows).ToHaveCountAsync(5);
        await Expect.That(rows).ToContainTextAsync(["Trail Backpack 24L", "Steel Water Bottle", "Merino Hiking Socks", "Camp Lantern", "Titanium Mug"]);
        await Expect.That(Screen.GetByTestId("product-backpack")).ToContainTextAsync("$120.00");
        await Expect.That(Screen.GetByTestId("product-backpack")).ToContainTextAsync("$49.00");
        await Expect.That(Screen.GetByTestId("product-bottle")).ToHaveTextAsync("Steel Water Bottle $25.00");
    }

    [Test]
    public async Task Low_to_high_sorts_by_the_original_price_not_the_paid_one_planted_bug()
    {
        var sort = Screen.GetByLabel("Sort by");
        await sort.SelectOptionAsync(new SelectOption { Value = "price-asc" });
        await Expect.That(sort).ToHaveValueAsync("price-asc");
        // Paid prices are socks 18, bottle 25, mug 12, lantern 54, backpack 49: a
        // correct sort opens with the $12 mug. The page sorts the struck prices.
        await Expect.That(Screen.GetByTestId("product-list").GetByRole("listitem")).ToContainTextAsync(
            ["Merino Hiking Socks", "Steel Water Bottle", "Titanium Mug", "Camp Lantern", "Trail Backpack 24L"]);
    }

    [Test]
    public async Task High_to_low_is_the_same_wrong_order_reversed_planted_bug()
    {
        var sort = Screen.GetByLabel("Sort by");
        await sort.SelectOptionAsync(new SelectOption { Value = "price-desc" });
        await Expect.That(sort).ToHaveValueAsync("price-desc");
        await Expect.That(Screen.GetByTestId("product-list").GetByRole("listitem")).ToContainTextAsync(
            ["Trail Backpack 24L", "Camp Lantern", "Titanium Mug", "Steel Water Bottle", "Merino Hiking Socks"]);
        await sort.SelectOptionAsync(new SelectOption { Value = "featured" });
        await Expect.That(Screen.GetByTestId("product-list").GetByRole("listitem").First()).ToContainTextAsync("Trail Backpack 24L");
    }
}
