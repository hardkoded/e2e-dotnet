// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using DaritenFixture;
using E2E.XUnit.V3;

namespace E2E.XUnit.V3.Sample;

/// <summary>
/// The tests of <c>samples/E2E.Sample</c>, written for xUnit v3.
/// <see cref="The_dashboard_links_to_the_transaction_register"/> serves a local copy of the
/// dashboard and the register. The public site at https://dariten.vercel.app renders those
/// pages from its database; when that database cannot start, the browser gets an error screen
/// with no Dashboard heading.
/// <see cref="An_agent_filters_the_register_by_category"/> still opens the live demo. The data
/// is shared, so that test only reads and never checks a specific balance or transaction.
/// </summary>
public sealed class DaritenTests : E2ETest
{
    [Fact]
    public async Task The_dashboard_links_to_the_transaction_register()
    {
        // The live page is an error screen while the Neon compute is over quota, so the
        // heading the demo's markup declares never reaches the browser. These pages keep
        // that markup's roles and names, and the walk from the dashboard into the register.
        await using var pages = DaritenFixtureHost.Start();
        await App.OpenAsync(pages.Url);
        await Expect.That(Screen.GetByRole("heading", "Dashboard")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("demo-banner")).ToContainTextAsync("Shared demo");
        await Expect.That(Screen.GetByText("Net worth")).ToBeVisibleAsync();

        await Screen.GetByRole("link", "Transactions").ClickAsync();
        await Expect.That(Screen.GetByRole("heading", "Transactions")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByRole("button", "Filter")).ToBeVisibleAsync();
    }

    [Fact]
    [Trait("TestCategory", "RealModel")]
    public async Task An_agent_filters_the_register_by_category()
    {
        await App.OpenAsync("/transactions");
        await Agent.ActAsync("filter the register to show only the Groceries category");
        await Agent.AssertAsync("every transaction listed is in the Groceries category, or the register says no transactions match");
        await Expect.That(Screen.GetByRole("heading", "Transactions")).ToBeVisibleAsync();
    }
}
