// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.NUnit.Tests.Forms;

/// <summary>
/// <c>test.describe('forms')</c> of upstream's <c>apps/testbed/tests/forms.e2e.ts</c>, against the playground's <c>/forms</c> page.
/// All four tests are ported. The class carries the <c>forms</c> category (upstream's <c>tags: ['forms']</c>).
/// </summary>
[Category("forms")]
public sealed class FormsTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    // Upstream's beforeEach. NUnit runs the base class's SetUp (which starts the session) before this one.
    [SetUp]
    public async Task OpenForms()
    {
        await App.OpenAsync("/forms");
    }

    [Test]
    public async Task Fills_clears_and_submits_a_profile()
    {
        await Screen.GetByLabel("Full name").FillAsync("Ada Lovelace");
        await Screen.GetByPlaceholder("Tell us about yourself").FillAsync("First programmer.");
        await Screen.GetByLabel("Team").SelectOptionAsync("Web");
        await Expect.That(Screen.GetByLabel("Team")).ToHaveValueAsync("web");
        await Screen.GetByLabel("Team").SelectOptionAsync(new E2E.SelectOption { Index = 2 });
        await Expect.That(Screen.GetByLabel("Team")).ToHaveValueAsync("mobile");

        var notifications = Screen.GetByRole("group", "Notifications");
        await Expect.That(notifications.GetByRole("checkbox")).ToHaveCountAsync(2);
        await Screen.GetByLabel("Email notifications").CheckAsync();
        await Screen.GetByLabel("Weekly digest").UncheckAsync();
        await Expect.That(Screen.GetByLabel("Email notifications")).ToBeCheckedAsync();
        await Expect.That(Screen.GetByLabel("Weekly digest")).Not.ToBeCheckedAsync();

        await Screen.GetByRole("button", "Save profile").TapAsync();
        await Expect.That(Screen.GetByRole("status", "Save result")).ToHaveTextAsync("Saved profile for Ada Lovelace");
    }

    [Test]
    public async Task Validates_required_fields()
    {
        await Screen.GetByLabel("Full name").FillAsync("temp");
        await Screen.GetByLabel("Full name").ClearAsync();
        await Screen.GetByRole("button", "Save profile").TapAsync();
        await Expect.That(Screen.GetByRole("status", "Save result")).ToHaveTextAsync("Name is required");
    }

    [Test]
    public async Task Reads_current_values_without_retrying()
    {
        var prefilled = Screen.GetByDisplayValue("prefilled-value");
        await Expect.That(prefilled).ToBeVisibleAsync();

        await Screen.GetByLabel("Full name").FillAsync("Grace");
        var value = await Screen.GetByLabel("Full name").InputValueAsync();
        Assert.That(value, Is.EqualTo("Grace"));
        var placeholder = await Screen.GetByLabel("Full name").GetAttributeAsync("placeholder");
        Assert.That(placeholder, Is.EqualTo("Ada Lovelace"));
    }

    [Test]
    public async Task Types_with_key_events_where_fill_leaves_the_app_cold()
    {
        var city = Screen.GetByLabel("City");
        var suggestions = Screen.GetByRole("list", "City suggestions").GetByRole("listitem");

        await city.FillAsync("W");
        await Expect.That(city).ToHaveValueAsync("W");
        await Expect.That(suggestions).ToHaveCountAsync(0);

        await city.ClearAsync();
        await city.PressSequentiallyAsync("Wa", new PressSequentiallyOptions { Delay = TimeSpan.FromMilliseconds(20) });
        await Expect.That(city).ToHaveValueAsync("Wa");
        await Expect.That(suggestions).ToHaveCountAsync(1);
        await Expect.That(suggestions.First()).ToHaveTextAsync("Warsaw");
    }
}
