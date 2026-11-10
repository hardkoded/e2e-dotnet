// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.Wishlist;

/// <summary>Ported from <c>describe('wishlist')</c> in upstream's <c>apps/web-benchmark/tests/wishlist.e2e.ts</c>.</summary>
public sealed class WishlistTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheWishlist() => await App.OpenAsync("/e/wishlist");

    [Test]
    public async Task Saving_toggles_the_button_and_the_tab_badge()
    {
        var planner = SaveButton("Weekly Desk Planner");
        var pen = SaveButton("Brass Fountain Pen");
        await Expect.That(Screen.GetByRole("button", "Wishlist (0)")).ToBeVisibleAsync();
        await Expect.That(planner).ToHaveTextAsync("♡ Save");

        await planner.TapAsync();
        await Expect.That(planner).ToHaveTextAsync("♥ Saved");
        await Expect.That(Screen.GetByRole("button", "Wishlist (1)")).ToBeVisibleAsync();

        await pen.TapAsync();
        await Expect.That(pen).ToHaveTextAsync("♥ Saved");
        await Expect.That(Screen.GetByRole("button", "Wishlist (2)")).ToBeVisibleAsync();

        await planner.TapAsync();
        await Expect.That(planner).ToHaveTextAsync("♡ Save");
        await Expect.That(Screen.GetByRole("button", "Wishlist (1)")).ToBeVisibleAsync();
    }

    [Test]
    public async Task The_wishlist_tab_renders_the_empty_state_despite_the_badge_count_planted_bug()
    {
        await SaveButton("Weekly Desk Planner").TapAsync();
        await SaveButton("Dot Grid Notebook").TapAsync();
        await Screen.GetByRole("button", "Wishlist (2)").TapAsync();

        await Expect.That(Screen.GetByTestId("shop-list")).ToBeHiddenAsync();
        await Expect.That(Screen.GetByTestId("wishlist-empty")).ToHaveTextAsync("No saved items yet.");
        await Expect.That(Screen.GetByTestId("wishlist-panel")).Not.ToContainTextAsync("Weekly Desk Planner");
        await Expect.That(Screen.GetByRole("button", "Wishlist (2)")).ToBeVisibleAsync();

        await Screen.GetByRole("button", "Shop").TapAsync();
        await Expect.That(SaveButton("Weekly Desk Planner")).ToHaveTextAsync("♥ Saved");
        await Expect.That(SaveButton("Brass Fountain Pen")).ToHaveTextAsync("♡ Save");
    }

    /// <summary>The Save button of the shop row that names the product.</summary>
    private Locator SaveButton(string product) =>
        Screen.GetByTestId("shop-list").GetByRole("listitem").Filter(product).GetByRole("button");
}
