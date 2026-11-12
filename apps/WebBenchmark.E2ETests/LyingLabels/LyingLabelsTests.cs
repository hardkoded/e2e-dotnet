// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.LyingLabels;

/// <summary>Ported from <c>describe('lying labels')</c> in upstream's <c>apps/web-benchmark/tests/lying-labels.e2e.ts</c>.</summary>
public sealed class LyingLabelsTests : BenchmarkTest
{
    private static readonly Regex PayNow = new("Pay now");

    [SetUp]
    public async Task OpenTheLyingLabels() => await App.OpenAsync("/e/lying-labels");

    [Test]
    public async Task The_button_whose_accessible_name_says_Pay_now_is_the_wrong_one()
    {
        await Screen.GetByRole("button", "Pay now").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("You clicked the wrong action");
    }

    [Test]
    public async Task A_text_match_for_Pay_now_catches_the_decoy_phrase_too()
    {
        var error = await Support.FailureAsync(() => Screen.GetByText(PayNow).TapAsync());
        Assert.That(error, Is.InstanceOf<E2EException>().And.Property(nameof(E2EException.Code)).EqualTo("LOCATOR_AMBIGUOUS"));
        await Expect.That(Screen.GetByTestId("error-message")).ToBeHiddenAsync();
    }

    [Test]
    public async Task The_field_whose_accessible_name_says_Amount_is_the_wrong_one()
    {
        await ButtonReading("Pay now").TapAsync();
        await Screen.GetByLabel("Amount").FillAsync("42.50");
        await Screen.GetByText("Submit payment").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message"))
            .ToHaveTextAsync("Type 42.50 into the field visibly labeled Amount, leave Reference empty");
    }

    [Test]
    public async Task Visible_text_drives_the_payment_through()
    {
        await ButtonReading("Pay now").TapAsync();
        await Support.SiblingOf(Browser, "Amount", "input").FillAsync("42.50");
        await Expect.That(Support.SiblingOf(Browser, "Reference", "input")).ToHaveValueAsync("");
        await Screen.GetByText("Submit payment").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Payment sent");
    }

    /// <summary>
    /// The button whose own text node reads <paramref name="text"/>. Each button here carries an aria-label that belongs to another button, and
    /// one hides a screen-reader span with a decoy phrase, so neither the accessible name nor the full text content names it truthfully; the
    /// rendered text does.
    /// </summary>
    private Locator ButtonReading(string text) =>
        Browser.Locator($"xpath=//button[normalize-space(text())=\"{text}\"]");
}
