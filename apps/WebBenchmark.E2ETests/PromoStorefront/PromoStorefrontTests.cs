// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.PromoStorefront;

/// <summary>Ported from the top-level test in upstream's <c>apps/web-benchmark/tests/promo-storefront.e2e.ts</c>, which has no <c>describe</c>.</summary>
public sealed class PromoStorefrontTests : BenchmarkTest
{
    [Test]
    public async Task Buys_a_deal_once_the_promo_chrome_is_out_of_the_way()
    {
        await App.OpenAsync("/e/promo-storefront");

        await Screen.GetByRole("button", "Accept").TapAsync();
        await Expect.That(Screen.GetByTestId("cookie-banner")).ToBeHiddenAsync();

        await Screen.GetByRole("button", "Open support chat").TapAsync();
        await Expect.That(Screen.GetByTestId("chat-panel")).ToContainTextAsync("Hi! How can we help?");
        await Screen.GetByRole("button", "Close").TapAsync();
        await Expect.That(Screen.GetByTestId("chat-panel")).ToBeHiddenAsync();

        // The review skeletons resolve 1.2 s after load; the assertion waits them out.
        await Expect.That(Screen.GetByTestId("reviews-section")).ToContainTextAsync("Great bass");

        var headphones = Screen.GetByTestId("deal-headphones");
        await Expect.That(headphones).ToContainTextAsync("$80.00");
        await Expect.That(headphones).ToContainTextAsync("$40.00");
        await Expect.That(headphones).ToContainTextAsync("50% OFF");
        await Expect.That(Screen.GetByTestId("deal-speaker")).ToContainTextAsync("30% OFF");

        await headphones.GetByRole("button", "Buy now").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Order confirmed");
        await Expect.That(headphones.GetByRole("button", "Buy now")).ToBeHiddenAsync();
        await Expect.That(Screen.GetByTestId("deal-speaker").GetByRole("button", "Buy now")).ToBeVisibleAsync();
    }
}
