// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.IframeForm;

/// <summary>Ported from <c>describe('iframe form')</c> in upstream's <c>apps/web-benchmark/tests/iframe-form.e2e.ts</c>.</summary>
public sealed class IframeFormTests : BenchmarkTest
{
    private static readonly Regex CouponCodeLabel = new("Your coupon code:");
    private static readonly Regex CouponCode = new(@"[A-Z]+-\d+");

    [SetUp]
    public async Task OpenTheIframeForm() => await App.OpenAsync("/e/iframe-form");

    [Test]
    public async Task A_wrong_code_is_refused_by_the_host_page()
    {
        var checkout = Browser.FrameLocator("iframe[title=\"checkout-frame\"]");
        await checkout.GetByPlaceholder("Coupon code").FillAsync("SAVE-0000");
        await checkout.GetByRole("button", "Apply").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Invalid coupon code");
    }

    [Test]
    public async Task The_code_read_from_one_frame_applies_through_the_other()
    {
        var coupon = Browser.FrameLocator("iframe[title=\"coupon-frame\"]").GetByText(CouponCodeLabel);
        var code = await Support.ReadCodeAsync(coupon, CouponCode);
        var checkout = Browser.FrameLocator("iframe[title=\"checkout-frame\"]");
        await checkout.GetByPlaceholder("Coupon code").FillAsync(code);
        await checkout.GetByRole("button", "Apply").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Coupon applied");
    }
}
