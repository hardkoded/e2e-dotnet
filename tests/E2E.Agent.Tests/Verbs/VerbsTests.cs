// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.NUnit;
using E2E.Playground;

namespace E2E.Agent.Tests.Verbs;

/// <summary>
/// The grammar verbs beyond tap and type against a real model, each on the playground control built for its
/// deterministic twin. Every step is pinned by a locator check, so a model that reached the goal another way
/// still passes and one that only claimed to does not. Ported from upstream's
/// <c>apps/testbed/tests-agent/verbs.e2e.ts</c>. Not ported: "upload attaches the named
/// project files", as this project has no fixture files to name yet.
/// </summary>
[Category("RealModel")]
public sealed class VerbsTests : E2ETest
{
    protected override string? BaseUrl => Testbed.Url;

    [Test]
    public async Task Hover_reveals_the_card_menu_and_the_agent_archives_the_card()
    {
        await Browser.GotoAsync("/board");
        await Agent.ActAsync("archive the Design review card; the archive action shows when the card actions are hovered");
        await Expect.That(Screen.GetByLabel("Board state")).ToHaveTextAsync("Design review is archived");
    }

    [Test]
    public async Task A_right_click_opens_the_file_menu_and_the_agent_renames_the_file()
    {
        await Browser.GotoAsync("/controls");
        await Agent.ActAsync("open the context menu of report.pdf with a right-click and choose Rename");
        await Expect.That(Screen.GetByRole("status", "File state")).ToHaveTextAsync("renamed");
    }

    [Test]
    public async Task A_long_press_and_a_double_tap_are_chosen_over_a_plain_tap()
    {
        await Browser.GotoAsync("/controls");
        var gesture = Screen.GetByRole("status", "Gesture state");
        await Agent.ActAsync("long-press the Hold me button until the gesture state reads long-pressed");
        await Expect.That(gesture).ToHaveTextAsync("long-pressed");
        await Agent.ActAsync("double-tap the Tap me twice button");
        await Expect.That(gesture).ToHaveTextAsync("double-tapped");
    }

    [Test]
    public async Task A_drag_moves_the_card_into_the_done_column()
    {
        await Browser.GotoAsync("/board");
        await Agent.ActAsync("move the Design review card into the Done column");
        await Expect.That(Screen.GetByLabel("Board state")).ToHaveTextAsync("Design review is done");
    }

    [Test]
    public async Task Check_sets_the_checkbox_and_the_radio_without_flipping_them_back()
    {
        await Browser.GotoAsync("/controls");
        await Agent.ActAsync("agree to the terms and pick the Medium size, then confirm both are set");
        await Expect.That(Screen.GetByLabel("Agree to terms")).ToBeCheckedAsync();
        await Expect.That(Screen.GetByRole("radio", "Medium")).ToBeCheckedAsync();
    }

    [Test]
    public async Task Scroll_to_brings_the_footnote_into_view()
    {
        await Browser.GotoAsync("/controls");
        await Agent.ActAsync("bring the Footnote paragraph at the bottom of the page into view");
        await Expect.That(Screen.GetByLabel("Footnote state")).ToHaveTextAsync("in view");
    }

    [Test]
    public async Task Back_returns_to_the_previous_page()
    {
        await Browser.GotoAsync("/controls");
        await Agent.ActAsync("open the Docs link, then go back to the controls page");
        await Expect.That(Browser).ToHaveURLAsync("/controls");
    }
}
