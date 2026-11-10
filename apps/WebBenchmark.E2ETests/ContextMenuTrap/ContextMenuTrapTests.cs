// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.ContextMenuTrap;

/// <summary>Ported from <c>describe('context menu trap')</c> in upstream's <c>apps/web-benchmark/tests/context-menu-trap.e2e.ts</c>.</summary>
public sealed class ContextMenuTrapTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheContextMenuTrap() => await App.OpenAsync("/e/context-menu-trap");

    [Test]
    public async Task Right_click_opens_nothing_the_row_menu_renames_the_file()
    {
        // The page advertises right-click but has no contextmenu handler.
        await Screen.GetByText("report.pdf").SecondaryTapAsync();
        await Expect.That(Screen.GetByRole("button", "Rename")).ToHaveCountAsync(0);

        await Screen.GetByRole("button", "More actions for report.pdf").TapAsync();
        await Screen.GetByRole("button", "Rename").TapAsync();
        var name = Screen.GetByLabel("New file name");
        await Expect.That(name).ToHaveValueAsync("report.pdf");
        await name.FillAsync("summary.pdf");
        await Screen.GetByRole("button", "Save").TapAsync();

        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("File renamed to summary.pdf");
        await Expect.That(Screen.GetByText("summary.pdf")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByText("report.pdf")).ToHaveCountAsync(0);
        await Expect.That(Screen.GetByText("quarterly-report.pdf")).ToBeVisibleAsync();
    }

    [Test]
    public async Task The_decoy_menu_items_refuse_and_keep_the_file()
    {
        var more = Screen.GetByRole("button", "More actions for report.pdf");
        await more.TapAsync();
        await Screen.GetByRole("button", "Duplicate").TapAsync();
        await Expect.That(Screen.GetByTestId("notice-message")).ToHaveTextAsync("Duplicating is disabled for sample files");

        await more.TapAsync();
        await Screen.GetByRole("button", "Delete").TapAsync();
        await Expect.That(Screen.GetByTestId("notice-message")).ToHaveTextAsync("Sample files cannot be deleted");
        await Expect.That(Screen.GetByText("report.pdf")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveCountAsync(0);
    }
}
