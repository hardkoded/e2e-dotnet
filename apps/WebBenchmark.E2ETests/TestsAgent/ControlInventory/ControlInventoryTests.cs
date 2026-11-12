// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace WebBenchmark.E2ETests.TestsAgent.ControlInventory;

/// <summary>
/// Ported from <c>describe('control inventory')</c> in upstream's <c>apps/web-benchmark/tests-agent/control-inventory.e2e.ts</c>: the agent
/// verbs the hard scenarios never reach, each on the plain control built for it. One step per verb, so a red test names the verb, and a
/// locator check pins each outcome the way the deterministic twin reads it.
/// </summary>
[Category("control-inventory")]
public sealed class ControlInventoryTests : TestsAgentTest
{
    [SetUp]
    public async Task OpenTheControlInventory() => await App.OpenAsync("/e/control-inventory");

    [Test]
    public async Task Check_sets_the_checkbox_and_the_radio()
    {
        await Agent.ActAsync("agree to the terms and pick the Medium size");
        await Expect.That(Screen.GetByLabel("Toggles state")).ToHaveTextAsync("terms agreed, size Medium");
    }

    [Test]
    public async Task Check_picks_a_radio_the_pick_replaces_with_its_summary()
    {
        await Agent.ActAsync("pick Express delivery");
        await Expect.That(Screen.GetByLabel("Delivery state")).ToHaveTextAsync("Express");
    }

    [Test]
    public async Task A_long_press_and_a_double_click_are_chosen_over_a_plain_tap()
    {
        await Agent.ActAsync("long-press the Hold me button until its state reads long-pressed, then double-click the Double-click me button");
        await Expect.That(Screen.GetByLabel("Hold state")).ToHaveTextAsync("long-pressed");
        await Expect.That(Screen.GetByLabel("Double-click state")).ToHaveTextAsync("double-clicked");
    }

    [Test]
    public async Task A_right_click_opens_the_file_menu_and_Rename_renames_the_file()
    {
        await Agent.ActAsync("rename report.pdf to summary.pdf; its menu opens on right-click");
        await Expect.That(Screen.GetByLabel("File state")).ToHaveTextAsync("report.pdf renamed to summary.pdf");
    }

    [Test]
    public async Task Upload_attaches_the_named_project_files()
    {
        await Agent.ActAsync(
            "attach both files to the Attachments input",
            new ActOptions { Params = new Dictionary<string, object?> { ["files"] = new[] { "Fixtures/attachment.txt", "Fixtures/second.txt" } } });
        await Expect.That(Screen.GetByLabel("Attachments state")).ToHaveTextAsync("attachment.txt, second.txt");
    }

    [Test]
    public async Task Back_returns_from_the_details_view_to_the_inventory()
    {
        await Agent.ActAsync("open the details, then return to the inventory through the browser history");
        await Expect.That(Browser).ToHaveURLAsync("/e/control-inventory");
        await Expect.That(Screen.GetByLabel("Navigation state")).ToHaveTextAsync("back on the inventory");
    }

    [Test]
    public async Task Scroll_to_brings_the_footnote_into_view()
    {
        await Agent.ActAsync("bring the Footnote paragraph at the bottom of the page into view");
        await Expect.That(Screen.GetByLabel("Footnote state")).ToHaveTextAsync("in view");
    }
}
