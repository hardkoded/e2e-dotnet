// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.SavedAddresses;

/// <summary>Ported from <c>describe('saved addresses')</c> in upstream's <c>apps/web-benchmark/tests/saved-addresses.e2e.ts</c>.</summary>
public sealed class SavedAddressesTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheSavedAddresses() => await App.OpenAsync("/e/saved-addresses");

    [Test]
    public async Task Adds_an_address_and_clears_the_form()
    {
        var rows = Screen.GetByTestId("address-list").GetByRole("listitem");
        await Expect.That(rows).ToHaveCountAsync(2);

        await Screen.GetByPlaceholder("Label (e.g. Cabin)").FillAsync("Cabin");
        await Screen.GetByPlaceholder("Street address").FillAsync("7 Lakeview Trail, Bend");
        await Screen.GetByRole("button", "Add address").TapAsync();

        await Expect.That(Screen.GetByTestId("toast")).ToHaveTextAsync("Address saved");
        await Expect.That(rows).ToHaveCountAsync(3);
        await Expect.That(rows.Last()).ToHaveTextAsync("Cabin 7 Lakeview Trail, Bend Delete");
        await Expect.That(Screen.GetByPlaceholder("Label (e.g. Cabin)")).ToHaveValueAsync("");
        await Expect.That(Screen.GetByPlaceholder("Street address")).ToHaveValueAsync("");
    }

    [Test]
    public async Task Ignores_an_add_with_an_empty_field()
    {
        await Screen.GetByPlaceholder("Label (e.g. Cabin)").FillAsync("Cabin");
        await Screen.GetByRole("button", "Add address").TapAsync();
        await Expect.That(Screen.GetByTestId("toast")).ToBeHiddenAsync();
        await Expect.That(Screen.GetByTestId("address-list").GetByRole("listitem")).ToHaveCountAsync(2);
        await Expect.That(Screen.GetByPlaceholder("Label (e.g. Cabin)")).ToHaveValueAsync("Cabin");
    }

    [Test]
    public async Task Delete_shows_its_toast_but_keeps_the_address_on_retry_too_planted_bug()
    {
        var rows = Screen.GetByTestId("address-list").GetByRole("listitem");
        var home = Screen.GetByTestId("address-home");
        var remove = home.GetByRole("button", "Delete");

        await remove.TapAsync();
        await Expect.That(Screen.GetByTestId("toast")).ToHaveTextAsync("Address deleted");
        await Expect.That(home).ToBeVisibleAsync();
        await Expect.That(rows).ToHaveCountAsync(2);

        await remove.TapAsync();
        await Expect.That(home).ToContainTextAsync("Home 12 Rosewood Lane, Portland");
        await Expect.That(rows).ToHaveCountAsync(2);
    }
}
