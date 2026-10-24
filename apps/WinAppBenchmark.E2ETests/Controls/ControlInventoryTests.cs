// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using WinAppBenchmark.E2ETests.Fixtures;

namespace WinAppBenchmark.E2ETests.Controls;

/// <summary>
/// Every deterministic verb, read, and matcher on the Control Inventory scenario: plain controls with every
/// state exposed, so a verb that fails here fails in the engine, never in the app. Each state assertion pairs
/// with the status line the app prints.
/// </summary>
public sealed class ControlInventoryTests : BenchmarkTest
{
    [SetUp]
    public async Task OpenTheInventory() => await OpenScenarioAsync("Control Inventory");

    /// <summary>Shows one of the scenario's sections; every control of it fits on screen.</summary>
    private async Task ShowSectionAsync(string name) => await Screen.GetByTestId($"tab-{name.ToLowerInvariant()}").TapAsync();

    [Test]
    public async Task A_label_query_resolves_a_labeled_text_field()
    {
        await Expect.That(Screen.GetByLabel("Name field")).ToHaveValueAsync("Ada Lovelace");
        await Expect.That(Screen.GetByLabel(new Regex("^Name"))).ToHaveAccessibleNameAsync("Name field");
    }

    [Test]
    public async Task A_labeled_field_answers_name_display_value_value_reads_fill_and_clear()
    {
        var name = Screen.GetByRole("textbox", "Name field");
        await Expect.That(name).ToHaveValueAsync("Ada Lovelace");
        await Expect.That(Screen.GetByDisplayValue("Ada Lovelace")).ToHaveAccessibleNameAsync("Name field");
        Assert.That(await name.InputValueAsync(), Is.EqualTo("Ada Lovelace"));

        await name.FillAsync("Grace Hopper");
        await Expect.That(name).ToHaveValueAsync("Grace Hopper");
        await Expect.That(Screen.GetByTestId("name-echo")).ToHaveTextAsync("name: Grace Hopper");

        await name.ClearAsync();
        await Expect.That(Screen.GetByTestId("name-echo")).ToHaveTextAsync("name:");
        await name.PressAsync("Enter");
    }

    [Test]
    public async Task A_secure_field_takes_a_fill_and_denies_value_reads()
    {
        var passphrase = Screen.GetByTestId("passphrase-input");
        await passphrase.FillAsync("correct horse");
        await Expect.That(Screen.GetByTestId("passphrase-length")).ToHaveTextAsync("passphrase length: 13");
        var denied = await Assert.ThrowsAsync<TestException>(async () => await passphrase.InputValueAsync());
        Assert.That(denied!.Code, Is.EqualTo("POLICY_DENIED"));
        var deniedTexts = await Assert.ThrowsAsync<TestException>(async () => await passphrase.AllTextContentsAsync());
        Assert.That(deniedTexts!.Code, Is.EqualTo("POLICY_DENIED"));
    }

    [Test]
    public async Task Press_sends_a_character_and_Enter_through_the_keyboard()
    {
        var key = Screen.GetByTestId("key-input");
        await key.PressAsync("a");
        await Expect.That(Screen.GetByTestId("key-status")).ToHaveTextAsync("key: a");
        await Expect.That(key).ToHaveValueAsync("a");
        await key.PressAsync("Enter");
        await Expect.That(Screen.GetByTestId("key-status")).ToHaveTextAsync("key: Enter");
    }

    [Test]
    public async Task A_field_autofocuses_on_mount_and_focus_moves_the_focus()
    {
        await Screen.GetByRole("button", "Show focus fields").TapAsync();
        var status = Screen.GetByTestId("focus-status");
        await Expect.That(status).ToHaveTextAsync("focus: Autofocus field");

        var second = Screen.GetByRole("textbox", "Second field");
        await second.FocusAsync();
        await Expect.That(status).ToHaveTextAsync("focus: Second field");

        await Screen.GetByRole("textbox", "Autofocus field").FocusAsync();
        await Expect.That(status).ToHaveTextAsync("focus: Autofocus field");
    }

    [Test]
    public async Task ToBeFocused_follows_the_focus()
    {
        await Screen.GetByRole("button", "Show focus fields").TapAsync();
        var first = Screen.GetByRole("textbox", "Autofocus field");
        var second = Screen.GetByRole("textbox", "Second field");
        await Expect.That(first).ToBeFocusedAsync();
        await second.FocusAsync();
        await Expect.That(second).ToBeFocusedAsync();
        await Expect.That(first).Not.ToBeFocusedAsync();
    }

    [Test]
    public async Task A_switch_checks_unchecks_and_reports_its_state()
    {
        await ShowSectionAsync("Toggles");
        var wifi = Screen.GetByTestId("wifi-switch");
        await Expect.That(wifi).ToHaveAccessibleNameAsync("Wi-Fi switch");
        Assert.That(await wifi.IsCheckedAsync(), Is.False);
        await Expect.That(wifi).Not.ToBeCheckedAsync();

        await wifi.CheckAsync();
        await Expect.That(wifi).ToBeCheckedAsync();
        Assert.That(await wifi.IsCheckedAsync(), Is.True);
        await Expect.That(Screen.GetByTestId("wifi-status")).ToHaveTextAsync("wifi: on");
        // Already on: a second check sends nothing and leaves it on.
        await wifi.CheckAsync();
        await Expect.That(Screen.GetByTestId("wifi-status")).ToHaveTextAsync("wifi: on");

        await wifi.UncheckAsync();
        await Expect.That(wifi).Not.ToBeCheckedAsync();
        await Expect.That(Screen.GetByTestId("wifi-status")).ToHaveTextAsync("wifi: off");
    }

    [Test]
    public async Task Checkbox_and_radio_roles_answer_GetByRole_and_a_checkbox_toggles_by_tap()
    {
        await ShowSectionAsync("Toggles");
        await Expect.That(Screen.GetByRole("radio")).ToHaveCountAsync(3);
        await Expect.That(Screen.GetByRole("radio", "Medium")).ToBeVisibleAsync();
        var newsletter = Screen.GetByRole("checkbox", "Newsletter");
        await Expect.That(newsletter).ToBeVisibleAsync();
        await Expect.That(Screen.GetByRole("checkbox")).ToHaveCountAsync(1);
        await Expect.That(Screen.GetByLabel("Newsletter")).ToHaveAccessibleNameAsync("Newsletter");

        await newsletter.TapAsync();
        await Expect.That(Screen.GetByTestId("newsletter-status")).ToHaveTextAsync("newsletter: on");
        await newsletter.TapAsync();
        await Expect.That(Screen.GetByTestId("newsletter-status")).ToHaveTextAsync("newsletter: off");
    }

    [Test]
    public async Task Checkbox_and_radio_roles_check_uncheck_and_report_their_state()
    {
        await ShowSectionAsync("Toggles");
        var newsletter = Screen.GetByRole("checkbox", "Newsletter");
        await Expect.That(newsletter).Not.ToBeCheckedAsync();
        Assert.That(await newsletter.IsCheckedAsync(), Is.False);

        await newsletter.CheckAsync();
        await Expect.That(newsletter).ToBeCheckedAsync();
        Assert.That(await newsletter.IsCheckedAsync(), Is.True);
        await Expect.That(Screen.GetByTestId("newsletter-status")).ToHaveTextAsync("newsletter: on");
        // Already on: a second check sends nothing and leaves it on.
        await newsletter.CheckAsync();
        await Expect.That(Screen.GetByTestId("newsletter-status")).ToHaveTextAsync("newsletter: on");

        await newsletter.UncheckAsync();
        await Expect.That(newsletter).Not.ToBeCheckedAsync();
        await Expect.That(Screen.GetByTestId("newsletter-status")).ToHaveTextAsync("newsletter: off");

        var small = Screen.GetByRole("radio", "Small");
        var medium = Screen.GetByRole("radio", "Medium");
        await Expect.That(small).ToBeCheckedAsync();
        await Expect.That(medium).Not.ToBeCheckedAsync();
        await medium.CheckAsync();
        await Expect.That(medium).ToBeCheckedAsync();
        await Expect.That(small).Not.ToBeCheckedAsync();
        await Expect.That(Screen.GetByTestId("size-status")).ToHaveTextAsync("size: Medium");
    }

    [Test]
    public async Task A_radio_group_reports_the_checked_option()
    {
        await ShowSectionAsync("Toggles");
        var sizes = Screen.GetByLabel(new Regex("^(Small|Medium|Large)$"));
        await Expect.That(sizes).ToHaveCountAsync(3);
        await Expect.That(Screen.GetByTestId("size-small")).ToBeCheckedAsync();
        await Expect.That(Screen.GetByTestId("size-medium")).Not.ToBeCheckedAsync();

        await Screen.GetByTestId("size-medium").TapAsync();
        await Expect.That(Screen.GetByTestId("size-medium")).ToBeCheckedAsync();
        await Expect.That(Screen.GetByTestId("size-small")).Not.ToBeCheckedAsync();
        await Expect.That(Screen.GetByTestId("size-status")).ToHaveTextAsync("size: Medium");
    }

    [Test]
    public async Task Buttons_report_disabled_enabled_and_a_state_that_arrives_later()
    {
        await ShowSectionAsync("Toggles");
        var locked = Screen.GetByRole("button", "Locked");
        await Expect.That(locked).ToBeDisabledAsync();
        Assert.That(await locked.IsDisabledAsync(), Is.True);
        Assert.That(await locked.IsEnabledAsync(), Is.False);

        var delayed = Screen.GetByRole("button", "Delayed");
        await Expect.That(delayed).ToBeEnabledAsync();
        Assert.That(await delayed.IsEnabledAsync(), Is.True);
        await Expect.That(Screen.GetByTestId("delayed-status")).ToHaveTextAsync("delayed: ready");
        await delayed.TapAsync();
        await Expect.That(Screen.GetByTestId("delayed-status")).ToHaveTextAsync("delayed: pressed");
    }

    // A collapsed element is left out of the UI Automation tree rather than marked hidden, so the ghost is
    // absent (count 0, not attached) and its twin the one match.
    [Test]
    public async Task A_collapsed_element_is_absent_rather_than_hidden_its_visible_twin_attached()
    {
        await ShowSectionAsync("Toggles");
        var ghost = Screen.GetByTestId("ghost-text");
        await Expect.That(ghost).ToBeHiddenAsync();
        Assert.That(await ghost.IsHiddenAsync(), Is.True);
        await Expect.That(ghost).Not.ToBeAttachedAsync();
        await Expect.That(ghost).ToHaveCountAsync(0);

        var twin = Screen.GetByTestId("twin-text");
        await Expect.That(twin).ToBeAttachedAsync();
        await Expect.That(twin).ToBeVisibleAsync();
        await Expect.That(Screen.GetByText("Now you see me")).ToHaveTextAsync("Now you see me");
        await Expect.That(Screen.GetByText("Now you see me")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task A_header_answers_its_accessible_name()
    {
        await Expect.That(Screen.GetByTestId("inventory-header")).ToHaveAccessibleNameAsync("Inventory heading");
        await Expect.That(Screen.GetByTestId("inventory-header")).ToHaveAccessibleNameAsync(new Regex("heading$"));
    }

    [Test]
    public async Task A_tab_row_answers_count_all_texts_filters_positions_and_switching()
    {
        var tabs = Screen.GetByLabel(new Regex("^(Fields|Toggles|Gestures)$"));
        await Expect.That(tabs).ToHaveCountAsync(3);
        Assert.That(await tabs.CountAsync(), Is.EqualTo(3));
        Assert.That(await tabs.AllTextContentsAsync(), Is.EqualTo(new[] { "Fields", "Toggles", "Gestures" }));
        await Expect.That(tabs).ToHaveTextAsync(new TextMatch[] { "Fields", "Toggles", "Gestures" });
        await Expect.That(tabs).ToContainTextAsync(new TextMatch[] { "Fie", "Tog", "Ges" });
        await Expect.That(tabs.First()).ToContainTextAsync("Fiel");
        await Expect.That(Screen.GetByTestId("name-input")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("wifi-status")).ToHaveCountAsync(0);

        await tabs.Nth(1).TapAsync();
        await Expect.That(Screen.GetByTestId("wifi-status")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("name-input")).ToHaveCountAsync(0);

        await tabs.Filter("Gestures").TapAsync();
        await Expect.That(Screen.GetByTestId("gesture-status")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("wifi-status")).ToHaveCountAsync(0);

        var each = await tabs.AllAsync();
        Assert.That(each, Has.Count.EqualTo(3));
        Assert.That(await each[2].TextContentAsync(), Is.EqualTo("Gestures"));
    }

    [Test]
    public async Task A_list_answers_counts_list_form_text_reads_and_boxes_in_order()
    {
        await ShowSectionAsync("Gestures");
        var rows = Screen.GetByTestId("fruit-row");
        await Expect.That(rows).ToHaveCountAsync(3);
        var names = Screen.GetByText(new Regex("^(Apple|Banana|Cherry)$"));
        await Expect.That(names).ToHaveTextAsync(new TextMatch[] { "Apple", "Banana", "Cherry" });
        await Expect.That(names).ToContainTextAsync(new TextMatch[] { "App", "nan", "err" });
        Assert.That(await names.AllTextContentsAsync(), Is.EqualTo(new[] { "Apple", "Banana", "Cherry" }));
        Assert.That(await names.Nth(1).TextContentAsync(), Is.EqualTo("Banana"));
        Assert.That(await names.First().TextContentAsync(), Is.EqualTo("Apple"));
        Assert.That(await names.Last().TextContentAsync(), Is.EqualTo("Cherry"));

        var page = Screen.GetByTestId("inventory-scroll");
        await Expect.That(page.Filter(Screen.GetByTestId("fruit-list"))).ToHaveCountAsync(1);
        await Expect.That(page.Filter("Cherry")).ToHaveCountAsync(1);
        await Expect.That(page.Filter("Durian")).ToHaveCountAsync(0);

        var first = await rows.First().BoundingBoxAsync();
        var last = await rows.Last().BoundingBoxAsync();
        Assert.That(first, Is.Not.Null);
        Assert.That(last, Is.Not.Null);
        Assert.That(first!.Height, Is.GreaterThan(0));
        Assert.That(first.Width, Is.GreaterThan(0));
        Assert.That(last!.Y, Is.GreaterThan(first.Y + first.Height - 1));
        Assert.That(last.X, Is.EqualTo(first.X));
    }

    [Test]
    public async Task A_long_press_lands_as_one()
    {
        await ShowSectionAsync("Gestures");
        await Session.LongPressAsync(await NodeAsync("long-press-target"), TimeSpan.FromMilliseconds(800));
        await Expect.That(Screen.GetByTestId("gesture-status")).ToHaveTextAsync("gesture: long press");
    }

    // The engine sends a double tap as a double click, which lands both presses inside the scenario's 300 ms window.
    [Test]
    public async Task A_double_tap_lands_inside_the_300_ms_window()
    {
        await ShowSectionAsync("Gestures");
        await Screen.GetByRole("button", "Double-tap me").DoubleTapAsync();
        await Expect.That(Screen.GetByTestId("tap-count")).ToContainTextAsync("2 taps");
        await Expect.That(Screen.GetByTestId("gesture-status")).ToHaveTextAsync("gesture: double tap");
    }

    [Test]
    public async Task An_ambiguous_locator_fails_at_once()
    {
        await ShowSectionAsync("Gestures");
        var ambiguous = await Assert.ThrowsAsync<TestException>(async () => await Screen.GetByTestId("fruit-row").TapAsync());
        Assert.That(ambiguous!.Code, Is.EqualTo("STRICT_MODE"));
    }

    [Test]
    public async Task Restart_and_clearState_reopen_the_app_on_its_home_list()
    {
        await App.RestartAsync();
        await Expect.That(Screen.GetByTestId("Login Form")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("inventory-header")).ToBeHiddenAsync();

        await App.ClearStateAsync();
        await Expect.That(Screen.GetByTestId("Login Form")).ToBeVisibleAsync();
    }

    // Not ported from upstream's `describe('control inventory')`:
    //  - 'a placeholder query and attribute reads find the field': a UI Automation edit exposes no placeholder.
    //  - 'a tab role answers getByRole' (android only): the inventory's tabs are buttons here.
    //  - 'a row contains its texts' (skipped upstream).
    //  - 'pointer verbs land at the point asked for': the locator API has no tapAt, tap position, or swipe.
    //  - the `app.screenshot` half of 'screenshot, restart, and clearState reopen the app on its home list': the port has no app.screenshot.
}
