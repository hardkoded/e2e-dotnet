// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WinAppBenchmark.E2ETests.Fixtures;

namespace WinAppBenchmark.E2ETests.FlowsLists;

/// <summary>The scenario flows that scroll, gesture, or wait: lists, the catalog, swipes, long presses, and refreshes.</summary>
public sealed class FlowsListsTests : BenchmarkTest
{
    [Test]
    public async Task Infinite_scroll_reaches_the_target_item()
    {
        await OpenScenarioAsync("Infinite Scroll List");
        var target = Screen.GetByTestId("item-137");
        await ScrollUntilVisibleAsync(target, maxSwipes: 120);
        await target.TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Found item 137");
    }

    [Test]
    public async Task Product_catalog_adds_exactly_two_of_the_right_variant()
    {
        await OpenScenarioAsync("Product Catalog");
        await Screen.GetByTestId("product-trail-mix-500").TapAsync();
        await Screen.GetByTestId("quantity-increase").TapAsync();
        await Expect.That(Screen.GetByTestId("quantity-value")).ToHaveTextAsync("2");
        await Screen.GetByTestId("add-to-cart").TapAsync();
        await Screen.GetByTestId("open-cart").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Order ready: 2 × Trail Mix 500 g");
    }

    // The long press and the swipe come from the window session: the locator API has no member for either.
    // The swipe is a drag from the card to the Back button, which lies to its left.
    [Test]
    public async Task Gestures_long_press_then_swipe_left()
    {
        await OpenScenarioAsync("Gestures");
        await Session.LongPressAsync(await NodeAsync("long-press-target"), TimeSpan.FromMilliseconds(1200));
        await Expect.That(Screen.GetByText("Step 2 of 3")).ToBeVisibleAsync();
        await Session.DragToAsync(await NodeAsync("swipe-target"), await NodeAsync("back"));
        await Expect.That(Screen.GetByText("Step 3 of 3")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Gestures_double_tap_completes_the_flow()
    {
        await OpenScenarioAsync("Gestures");
        await Session.LongPressAsync(await NodeAsync("long-press-target"), TimeSpan.FromMilliseconds(1200));
        await Session.DragToAsync(await NodeAsync("swipe-target"), await NodeAsync("back"));
        await Screen.GetByTestId("double-tap-target").DoubleTapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("All gestures completed");
    }

    // The footer Continue button is a decoy until the terms are accepted; the accept button sits in the tree
    // off screen from the first snapshot.
    [Test]
    public async Task Sticky_chrome_accept_below_the_fold_then_continue()
    {
        await OpenScenarioAsync("Sticky Chrome Target");
        await Screen.GetByTestId("continue-button").TapAsync();
        await Expect.That(Screen.GetByText("Accept the terms at the bottom first")).ToBeVisibleAsync();
        var accept = Screen.GetByTestId("accept-terms");
        await ScrollUntilVisibleAsync(accept);
        await accept.TapAsync();
        await Expect.That(Screen.GetByText("Terms accepted ✓")).ToBeVisibleAsync();
        await Screen.GetByTestId("continue-button").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Terms accepted");
    }

    // A mouse has no pull to refresh: the Refresh button stands in for it.
    [Test]
    public async Task Async_states_load_refresh_claim_through_the_toast()
    {
        await OpenScenarioAsync("Async States");
        await Expect.That(Screen.GetByTestId("initial-loading")).ToBeVisibleAsync();
        var content = Screen.GetByText("Refresh to reveal your reward");
        await Expect.That(content).ToBeVisibleAsync();
        await Screen.GetByTestId("refresh-button").TapAsync();
        var claim = Screen.GetByTestId("claim-button");
        await Expect.That(claim).ToBeVisibleAsync();
        // The toast is gone within a couple of seconds; the message it leaves behind is the claim.
        await claim.TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Reward claimed");
    }

    // Row 512 is about 28,000 px down. Only the rows on screen enter the tree, so the count is a screenful, never the 600 loaded.
    [Test]
    [CancelAfter(300_000)]
    public async Task Huge_virtualized_list_reaches_row_512()
    {
        await OpenScenarioAsync("Huge Virtualized List");
        var rows = Screen.GetByTestId("huge-list").GetByText(new System.Text.RegularExpressions.Regex(@"^Row \d{4}$"));
        await Expect.That(rows.First()).ToHaveTextAsync("Row 0001");
        var onScreen = await rows.CountAsync();
        Assert.That(onScreen, Is.GreaterThan(0));
        Assert.That(onScreen, Is.LessThan(600));
        var target = Screen.GetByTestId("row-512");
        await ScrollUntilVisibleAsync(target, maxSwipes: 400);
        await target.TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Found Row 0512");
    }

    // Not ported: 'web view coupon form applies the code shown on the page'. The app has no WebView Accessibility scenario.
}
