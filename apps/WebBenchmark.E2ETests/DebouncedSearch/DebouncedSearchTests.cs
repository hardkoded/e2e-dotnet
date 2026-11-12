// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.DebouncedSearch;

/// <summary>Ported from <c>describe('debounced search')</c> in upstream's <c>apps/web-benchmark/tests/debounced-search.e2e.ts</c>.</summary>
public sealed class DebouncedSearchTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenAndSearch()
    {
        await App.OpenAsync("/e/debounced-search");
        // Every keystroke resets the debounce; the portal listbox opens once after
        // the last one plus the fake search round trip.
        await Screen.GetByRole("combobox").PressSequentiallyAsync("trail mix");
        await Expect.That(Screen.GetByRole("combobox")).ToBeExpandedAsync();
        await Expect.That(Screen.GetByRole("option")).ToHaveCountAsync(5);
    }

    [Test]
    public async Task A_decoy_one_character_off_is_refused()
    {
        await Screen.GetByRole("option", "Trail Mix 500 mg").TapAsync();
        await Expect.That(Screen.GetByTestId("error-message")).ToHaveTextAsync("Wrong item, look closer");
        await Expect.That(Screen.GetByRole("listbox")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Picking_the_exact_item_adds_it_to_the_cart()
    {
        await Screen.GetByRole("option", "Trail Mix 500 g").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Added Trail Mix 500 g to the cart");
    }
}
