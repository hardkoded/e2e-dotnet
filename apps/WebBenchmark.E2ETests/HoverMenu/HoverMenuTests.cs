// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.HoverMenu;

/// <summary>Ported from <c>describe('hover menu')</c> in upstream's <c>apps/web-benchmark/tests/hover-menu.e2e.ts</c>.</summary>
public sealed class HoverMenuTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheHoverMenu() => await App.OpenAsync("/e/hover-menu");

    [Test]
    public async Task A_decoy_leaf_is_refused()
    {
        await Screen.GetByText("Account").HoverAsync();
        await Screen.GetByText("Sign out").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("That is not the action");
    }

    [Test]
    public async Task Hovering_Account_then_Billing_reaches_Redeem_voucher()
    {
        await Screen.GetByText("Account").HoverAsync();
        await Screen.GetByText("Billing ›").HoverAsync();
        await Screen.GetByText("Redeem voucher").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Voucher redeemed");
    }
}
