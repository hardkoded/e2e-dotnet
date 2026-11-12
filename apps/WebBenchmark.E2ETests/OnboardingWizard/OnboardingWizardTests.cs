// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.OnboardingWizard;

/// <summary>Ported from <c>describe('onboarding wizard')</c> in upstream's <c>apps/web-benchmark/tests/onboarding-wizard.e2e.ts</c>.</summary>
public sealed class OnboardingWizardTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheOnboardingWizard()
    {
        await App.OpenAsync("/e/onboarding-wizard");
        await Expect.That(Screen.GetByText("Step 1 of 5")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Back_keeps_what_was_typed_and_the_brief_comes_back_with_it()
    {
        await Screen.GetByText("Next").TapAsync();
        await Expect.That(Screen.GetByText("Full name does not match the brief")).ToBeVisibleAsync();

        await CompleteAccountAsync();
        await Expect.That(Screen.GetByText("Support PIN: 8412")).ToBeHiddenAsync();

        await Screen.GetByText("Back").TapAsync();
        await Expect.That(Screen.GetByText("Step 1 of 5")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByText("Support PIN: 8412")).ToBeVisibleAsync();
        await Expect.That(Support.SiblingOf(Browser, "Full name", "input")).ToHaveValueAsync("Maria Novak");
        await Expect.That(Support.SiblingOf(Browser, "Phone", "input")).ToHaveValueAsync("+48 601 222 333");
    }

    [Test]
    public async Task The_address_step_takes_the_brief_through_its_bare_div_dropdown()
    {
        await CompleteAccountAsync();
        await CompleteAddressAsync();
    }

    [Test]
    public async Task The_profile_step_steers_the_calendar_three_decades_back()
    {
        await CompleteAccountAsync();
        await CompleteAddressAsync();
        await CompleteProfileAsync();
    }

    [Test]
    public async Task The_preferences_step_flips_the_toggles_the_brief_names_and_leaves_the_trap_off()
    {
        await CompleteAccountAsync();
        await CompleteAddressAsync();
        await CompleteProfileAsync();
        await CompletePreferencesAsync();
    }

    [Test]
    public async Task The_review_step_repeats_the_brief_and_finishes_with_the_PIN()
    {
        await CompleteAccountAsync();
        await CompleteAddressAsync();
        await CompleteProfileAsync();
        await CompletePreferencesAsync();

        await Expect.That(Screen.GetByText("Maria Novak · maria.novak@example.com · +48 601 222 333")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByText("Krucza 12, 00-585 Warsaw, Poland")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByText("Born March 14, 1990 · Product designer · 7 years")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByText("Plan: Studio")).ToBeVisibleAsync();
        await Support.SiblingOf(Browser, "Support PIN from the brief", "input").FillAsync("8412");
        await Support.SiblingOf(Browser, "I confirm the details are correct", "div").TapAsync();
        await Screen.GetByText("Finish").TapAsync();

        await Expect.That(Screen.GetByText("Onboarding complete")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByText("Welcome aboard, Maria Novak")).ToBeVisibleAsync();
    }

    /// <summary>Fills the Account step from the brief and moves on to Address.</summary>
    private async Task CompleteAccountAsync()
    {
        await Support.SiblingOf(Browser, "Full name", "input").FillAsync("Maria Novak");
        await Support.SiblingOf(Browser, "Email", "input").FillAsync("maria.novak@example.com");
        await Support.SiblingOf(Browser, "Phone", "input").FillAsync("+48 601 222 333");
        await Screen.GetByText("Next").TapAsync();
        await Expect.That(Screen.GetByText("Step 2 of 5")).ToBeVisibleAsync();
    }

    /// <summary>Fills the Address step, picks the country from its bare-div dropdown, and moves on to Profile.</summary>
    private async Task CompleteAddressAsync()
    {
        await Support.SiblingOf(Browser, "Street", "input").FillAsync("Krucza 12");
        await Support.SiblingOf(Browser, "City", "input").FillAsync("Warsaw");
        await Support.SiblingOf(Browser, "Postal code", "input").FillAsync("00-585");
        var country = Support.SiblingOf(Browser, "Country", "div");
        await country.TapAsync();
        await Screen.GetByText("Poland").TapAsync();
        await Expect.That(country).ToHaveTextAsync("Poland");
        await Screen.GetByText("Next").TapAsync();
        await Expect.That(Screen.GetByText("Step 3 of 5")).ToBeVisibleAsync();
    }

    /// <summary>Steers the calendar from June 2026 to March 14, 1990, picks the occupation, counts the stepper to 7, and moves on to Preferences.</summary>
    private async Task CompleteProfileAsync()
    {
        await Expect.That(Screen.GetByText("June 2026")).ToBeVisibleAsync();
        await TapTimesAsync(Screen.GetByText("«"), 3);
        await TapTimesAsync(Screen.GetByText("‹"), 6);
        await TapTimesAsync(Screen.GetByText("‹ month"), 3);
        await Expect.That(Screen.GetByText("March 1990")).ToBeVisibleAsync();
        await Screen.GetByText("14").TapAsync();
        await Expect.That(Screen.GetByText("Selected: March 14, 1990")).ToBeVisibleAsync();
        var occupation = Support.SiblingOf(Browser, "Occupation", "div");
        await occupation.TapAsync();
        await Screen.GetByText("Product designer").TapAsync();
        await Expect.That(occupation).ToHaveTextAsync("Product designer");
        var stepper = Support.SiblingOf(Browser, "Years of experience", "div");
        await TapTimesAsync(stepper.GetByText("+"), 7);
        await Expect.That(stepper.GetByText("7")).ToBeVisibleAsync();
        await Screen.GetByText("Next").TapAsync();
        await Expect.That(Screen.GetByText("Step 4 of 5")).ToBeVisibleAsync();
    }

    /// <summary>Flips the toggles the brief names, leaves the trap off, picks the plan, and moves on to Review.</summary>
    private async Task CompletePreferencesAsync()
    {
        await Support.SiblingOf(Browser, "Product updates", "div").TapAsync();
        await Support.SiblingOf(Browser, "SMS alerts", "div").TapAsync();
        await Support.SiblingOf(Browser, "Weekly digest", "div").TapAsync();
        await Expect.That(Support.SiblingOf(Browser, "Product updates", "div")).ToHaveTextAsync("On");
        await Expect.That(Support.SiblingOf(Browser, "SMS alerts", "div")).ToHaveTextAsync("Off");
        await Expect.That(Support.SiblingOf(Browser, "Weekly digest", "div")).ToHaveTextAsync("On");
        await Expect.That(Support.SiblingOf(Browser, "Beta features", "div")).ToHaveTextAsync("Off");
        await Screen.GetByText("Studio").TapAsync();
        await Screen.GetByText("Next").TapAsync();
        await Expect.That(Screen.GetByText("Step 5 of 5")).ToBeVisibleAsync();
    }

    /// <summary>Taps a bare-div button by its visible text <paramref name="times"/> times.</summary>
    private static async Task TapTimesAsync(Locator target, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await target.TapAsync();
        }
    }
}
