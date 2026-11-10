// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.ControlInventory;

/// <summary>
/// Ported from <c>describe('control inventory')</c> in upstream's <c>apps/web-benchmark/tests/control-inventory.e2e.ts</c>: the verbs the
/// hard scenarios never reach, each on the plain control built for it.
/// </summary>
public sealed class ControlInventoryTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheControlInventory() => await App.OpenAsync("/e/control-inventory");

    [Test]
    public async Task Check_sets_the_checkbox_and_the_radio_without_flipping_a_set_one()
    {
        var terms = Screen.GetByLabel("Agree to terms");
        await terms.CheckAsync();
        await terms.CheckAsync();
        await Expect.That(terms).ToBeCheckedAsync();
        await Screen.GetByRole("radio", "Medium").CheckAsync();
        await Expect.That(Screen.GetByLabel("Toggles state")).ToHaveTextAsync("terms agreed, size Medium");
        await terms.UncheckAsync();
        await Expect.That(Screen.GetByLabel("Toggles state")).ToHaveTextAsync("terms not agreed, size Medium");
    }

    [Test]
    public async Task Check_picks_a_radio_the_pick_replaces_with_its_summary()
    {
        await Screen.GetByRole("radio", "Express").CheckAsync();
        await Expect.That(Screen.GetByLabel("Delivery state")).ToHaveTextAsync("Express");
        await Expect.That(Screen.GetByText("Express delivery selected")).ToBeVisibleAsync();
    }

    [Test]
    public async Task A_long_press_and_a_double_click_are_told_apart_from_a_tap()
    {
        var hold = Screen.GetByRole("button", "Hold me");
        await hold.TapAsync();
        await Expect.That(Screen.GetByLabel("Hold state")).ToHaveTextAsync("tapped");
        await hold.LongPressAsync(new LongPressOptions { Duration = TimeSpan.FromMilliseconds(700) });
        await Expect.That(Screen.GetByLabel("Hold state")).ToHaveTextAsync("long-pressed");
        var twice = Screen.GetByRole("button", "Double-click me");
        await twice.TapAsync();
        await Expect.That(Screen.GetByLabel("Double-click state")).ToHaveTextAsync("clicked once");
        await twice.DoubleTapAsync();
        await Expect.That(Screen.GetByLabel("Double-click state")).ToHaveTextAsync("double-clicked");
    }

    [Test]
    public async Task The_file_menu_opens_on_right_click_only_and_Rename_renames_the_file()
    {
        var row = Screen.GetByText("report.pdf");
        await row.TapAsync();
        await Expect.That(Screen.GetByLabel("File state")).ToHaveTextAsync("a click selects nothing; the menu opens on right-click");
        await row.SecondaryTapAsync();
        await Expect.That(Screen.GetByLabel("File state")).ToHaveTextAsync("menu open for report.pdf");
        await Screen.GetByRole("menuitem", "Rename").TapAsync();
        await Expect.That(Screen.GetByLabel("File state")).ToHaveTextAsync("report.pdf renamed to summary.pdf");
        await Expect.That(Screen.GetByText("summary.pdf")).ToBeVisibleAsync();
    }

    [Test]
    public async Task SetInputFiles_attaches_files_named_from_the_project_root()
    {
        await Screen.GetByLabel("Attachments").SetInputFilesAsync(["Fixtures/attachment.txt", "Fixtures/second.txt"]);
        await Expect.That(Screen.GetByLabel("Attachments state")).ToHaveTextAsync("attachment.txt, second.txt");
    }

    [Test]
    public async Task The_details_view_is_left_through_the_browser_history()
    {
        await Screen.GetByRole("button", "Open details").TapAsync();
        await Expect.That(Browser).ToHaveURLAsync("/e/control-inventory?view=details");
        await Expect.That(Screen.GetByLabel("Navigation state")).ToHaveTextAsync("on details");
        await App.BackAsync();
        await Expect.That(Browser).ToHaveURLAsync("/e/control-inventory");
        await Expect.That(Screen.GetByLabel("Navigation state")).ToHaveTextAsync("back on the inventory");
    }

    [Test]
    public async Task ScrollIntoView_brings_the_footnote_into_view()
    {
        await Expect.That(Screen.GetByLabel("Footnote state")).ToHaveTextAsync("out of view");
        await Screen.GetByText("Footnote").ScrollIntoViewAsync();
        await Expect.That(Screen.GetByLabel("Footnote state")).ToHaveTextAsync("in view");
    }

    [Test]
    public async Task Every_exercise_done_shows_the_success_message()
    {
        await Screen.GetByLabel("Agree to terms").CheckAsync();
        await Screen.GetByRole("radio", "Medium").CheckAsync();
        await Screen.GetByRole("radio", "Express").CheckAsync();
        await Screen.GetByRole("button", "Hold me").LongPressAsync(new LongPressOptions { Duration = TimeSpan.FromMilliseconds(700) });
        await Screen.GetByRole("button", "Double-click me").DoubleTapAsync();
        await Screen.GetByText("report.pdf").SecondaryTapAsync();
        await Screen.GetByRole("menuitem", "Rename").TapAsync();
        await Screen.GetByLabel("Attachments").SetInputFilesAsync("Fixtures/attachment.txt");
        await Screen.GetByRole("button", "Open details").TapAsync();
        await Expect.That(Screen.GetByLabel("Navigation state")).ToHaveTextAsync("on details");
        await App.BackAsync();
        await Expect.That(Screen.GetByLabel("Navigation state")).ToHaveTextAsync("back on the inventory");
        await Screen.GetByText("Footnote").ScrollIntoViewAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("All exercises done");
    }
}
