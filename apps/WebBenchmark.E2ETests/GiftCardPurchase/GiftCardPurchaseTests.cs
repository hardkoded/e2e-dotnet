// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.GiftCardPurchase;

/// <summary>
/// Ported from <c>describe('gift card purchase')</c> in upstream's <c>apps/web-benchmark/tests/gift-card-purchase.e2e.ts</c>. The accessible
/// names are the trap here (aria-labels reading "undefined" and raw i18n keys), so the controls go by test id and placeholder.
/// </summary>
public sealed class GiftCardPurchaseTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheGiftCardPurchase() => await App.OpenAsync("/e/gift-card-purchase");

    [Test]
    public async Task Refuses_to_buy_without_an_amount()
    {
        await Screen.GetByTestId("buy-button").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Choose a gift card amount");
    }

    [Test]
    public async Task Refuses_a_recipient_without_an_at_sign()
    {
        await Screen.GetByTestId("amount-10").TapAsync();
        await Screen.GetByPlaceholder("friend@example.com").FillAsync("friend");
        await Screen.GetByTestId("buy-button").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Enter a valid recipient email");
    }

    [Test]
    public async Task Sends_the_gift_card()
    {
        await Expect.That(Screen.GetByTestId("total-amount")).ToHaveTextAsync("$0.00");
        await Screen.GetByTestId("amount-25").TapAsync();
        await Expect.That(Screen.GetByTestId("total-amount")).ToHaveTextAsync("$25.00");
        await Screen.GetByPlaceholder("friend@example.com").FillAsync("friend@example.com");
        await Screen.GetByTestId("buy-button").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Gift card sent to friend@example.com");
        await Expect.That(Screen.GetByText("A $25.00 gift card is on its way.")).ToBeVisibleAsync();
    }
}
