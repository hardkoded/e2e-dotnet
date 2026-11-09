// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.OrderHistory;

/// <summary>Ported from <c>describe('order history')</c> in upstream's <c>apps/web-benchmark/tests/order-history.e2e.ts</c>.</summary>
public sealed class OrderHistoryTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheOrderHistory() => await App.OpenAsync("/e/order-history");

    [Test]
    public async Task Overview_and_settings_render_their_panels()
    {
        await Expect.That(Screen.GetByTestId("overview-panel")).ToContainTextAsync("Welcome back, Alex");
        await Screen.GetByRole("button", "Settings").TapAsync();
        await Expect.That(Screen.GetByTestId("overview-panel")).ToBeHiddenAsync();
        await Expect.That(Screen.GetByTestId("settings-panel")).ToContainTextAsync("Email: alex@example.com");
        await Screen.GetByRole("button", "Overview").TapAsync();
        await Expect.That(Screen.GetByTestId("overview-panel")).ToContainTextAsync("You have 3 orders and 1 active subscription.");
    }

    [Test]
    public async Task The_orders_tab_never_leaves_its_loading_state_planted_bug()
    {
        await Screen.GetByRole("button", "Orders").TapAsync();
        var orders = Screen.GetByTestId("orders-panel");
        // A negated matcher passes only after 1000 ms of continuous truth: the
        // dwell that tells a stuck loading state from a slow one.
        await Expect.That(orders.GetByText("Loading your orders...")).Not.ToBeHiddenAsync();
        await Expect.That(orders.GetByRole("listitem")).ToHaveCountAsync(0);

        await Screen.GetByRole("button", "Settings").TapAsync();
        await Expect.That(Screen.GetByTestId("settings-panel")).ToBeVisibleAsync();
        await Screen.GetByRole("button", "Orders").TapAsync();
        await Expect.That(orders).ToHaveTextAsync("Loading your orders...");
    }
}
