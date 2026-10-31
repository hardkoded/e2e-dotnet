// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.WinApp;
using E2E.WinApp.Tests.Helpers;

namespace E2E.WinApp.Tests.Engine;

/// <summary>.NET-only: upstream has no Windows engine. Each contract action as the driver calls it makes.</summary>
public sealed class PerformTests
{
    private static async Task<(Harness Harness, WindowSession Session, Observation Observation)> OpenAsync(WinAppOptions? options = null, RawElement? tree = null)
    {
        var harness = new Harness(options);
        var session = await harness.OpenAsync(tree);
        var observation = await session.ObserveAsync(CancellationToken.None);
        harness.Driver.Calls.Clear();
        harness.Settles.Clear();
        return (harness, session, observation);
    }

    [Fact]
    public async Task A_tap_on_an_invokable_node_is_its_own_pattern_and_waits_for_nothing()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        await session.PerformAsync(Harness.ByTestId(observation, "save-button"), new LocatorAction.Tap(), CancellationToken.None);
        Assert.Equal(["invoke btn-save Auto"], harness.Driver.Calls);
        Assert.Empty(harness.Settles);
    }

    [Fact]
    public async Task A_tap_on_anything_else_is_a_click_on_its_middle_in_the_foreground_window_then_a_settle()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        await session.PerformAsync(Harness.ByName(observation, "Settings are saved on exit"), new LocatorAction.Tap(), CancellationToken.None);
        Assert.Equal([$"foreground {FakeWindowDriver.MainHandle}", "click 270,30"], harness.Driver.Calls);
        Assert.Equal([TimeSpan.FromMilliseconds(WinAppOptions.DefaultSettleMilliseconds)], harness.Settles);
    }

    [Fact]
    public async Task A_double_tap_is_a_double_click()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        await session.PerformAsync(Harness.ByTestId(observation, "save-button"), new LocatorAction.DoubleTap(), CancellationToken.None);
        Assert.Equal([$"foreground {FakeWindowDriver.MainHandle}", "click 540,615 double"], harness.Driver.Calls);
    }

    [Fact]
    public async Task A_tap_on_a_hidden_node_is_not_actionable()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(Harness.ByTestId(observation, "hidden-button"), new LocatorAction.DoubleTap(), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.NotActionable, error.Code);
        Assert.Contains("off the screen", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Settle_can_be_switched_off()
    {
        var (harness, session, observation) = await OpenAsync(Harness.Options(settle: false));
        await using var _ = harness;
        await session.PerformAsync(Harness.ByName(observation, "Settings are saved on exit"), new LocatorAction.Tap(), CancellationToken.None);
        Assert.Empty(harness.Settles);
    }

    [Fact]
    public async Task A_fill_sets_the_value_through_the_pattern()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        await session.PerformAsync(Harness.ByTestId(observation, "name-input"), new LocatorAction.Fill("Grace"), CancellationToken.None);
        Assert.Equal(["setValue edit-name Grace"], harness.Driver.Calls);
    }

    [Fact]
    public async Task A_fill_the_pattern_refuses_goes_through_the_keyboard()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        harness.Driver.RejectsValue.Add("edit-name");
        await session.PerformAsync(Harness.ByTestId(observation, "name-input"), new LocatorAction.Fill("Grace"), CancellationToken.None);
        Assert.Equal(
            ["setValue edit-name Grace", "focus edit-name", $"foreground {FakeWindowDriver.MainHandle}", $"sendKeys {FakeWindowDriver.MainHandle} ctrl+a", $"sendKeys {FakeWindowDriver.MainHandle} verbatim:Grace"],
            harness.Driver.Calls);
        Assert.Single(harness.Settles);
    }

    [Fact]
    public async Task A_clear_is_an_empty_fill_and_deletes_through_the_keyboard_when_refused()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        await session.PerformAsync(Harness.ByTestId(observation, "name-input"), new LocatorAction.Clear(), CancellationToken.None);
        Assert.Equal(["setValue edit-name "], harness.Driver.Calls);
        harness.Driver.Calls.Clear();
        harness.Driver.RejectsValue.Add("edit-name");
        await session.PerformAsync(Harness.ByTestId(observation, "name-input"), new LocatorAction.Clear(), CancellationToken.None);
        Assert.Contains($"sendKeys {FakeWindowDriver.MainHandle} delete", harness.Driver.Calls);
    }

    [Fact]
    public async Task A_sensitive_fill_never_puts_its_value_in_the_error()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        harness.Driver.Faults["setValue"] = new WindowDriverException(DriverFault.Other, "cannot set 'hunter2-secret' on the element");
        var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(Harness.ByTestId(observation, "password-input"), new LocatorAction.Fill("hunter2-secret", Sensitive: true), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.EngineFailure, error.Code);
        Assert.DoesNotContain("hunter2", error.Message, StringComparison.Ordinal);
        Assert.Contains("[redacted]", error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task A_press_focuses_the_node_and_sends_the_key_in_the_library_grammar()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        await session.PerformAsync(Harness.ByTestId(observation, "name-input"), new LocatorAction.Press("Control+Enter"), CancellationToken.None);
        Assert.Equal(["focus edit-name", $"foreground {FakeWindowDriver.MainHandle}", $"sendKeys {FakeWindowDriver.MainHandle} ctrl+enter"], harness.Driver.Calls);
        Assert.Single(harness.Settles);
    }

    [Fact]
    public async Task A_press_of_one_character_types_it_literally()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        await session.PerformAsync(Harness.ByTestId(observation, "name-input"), new LocatorAction.Press("é"), CancellationToken.None);
        Assert.Equal(["focus edit-name", $"foreground {FakeWindowDriver.MainHandle}", $"sendKeys {FakeWindowDriver.MainHandle} verbatim:é"], harness.Driver.Calls);
    }

    [Fact]
    public async Task A_press_outside_the_grammar_is_unsupported_before_any_driver_call()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(Harness.ByTestId(observation, "name-input"), new LocatorAction.Press("Hyper+Q"), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, error.Code);
        Assert.Empty(harness.Driver.Calls);
    }

    [Fact]
    public async Task Pressing_sequentially_types_the_text_literally_into_the_focused_node()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        await session.PerformAsync(Harness.ByTestId(observation, "name-input"), new LocatorAction.PressSequentially("ab c"), CancellationToken.None);
        Assert.Equal(["focus edit-name", $"foreground {FakeWindowDriver.MainHandle}", $"sendKeys {FakeWindowDriver.MainHandle} verbatim:ab c"], harness.Driver.Calls);
    }

    [Fact]
    public async Task The_session_press_goes_to_the_main_window()
    {
        var (harness, session, _) = await OpenAsync();
        await using var __ = harness;
        await session.PressAsync("Escape", CancellationToken.None);
        Assert.Equal([$"foreground {FakeWindowDriver.MainHandle}", $"sendKeys {FakeWindowDriver.MainHandle} esc"], harness.Driver.Calls);
    }

    [Fact]
    public async Task Check_and_uncheck_toggle_only_when_the_state_differs()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        var terms = Harness.ByName(observation, "Accept terms");
        var dark = Harness.ByName(observation, "Dark mode");
        await session.PerformAsync(terms, new LocatorAction.Check(), CancellationToken.None);
        await session.PerformAsync(dark, new LocatorAction.Uncheck(), CancellationToken.None);
        Assert.Empty(harness.Driver.Calls);
        await session.PerformAsync(terms, new LocatorAction.Uncheck(), CancellationToken.None);
        await session.PerformAsync(dark, new LocatorAction.Check(), CancellationToken.None);
        Assert.Equal(["invoke chk-terms ToggleOff", "invoke tog-dark ToggleOn"], harness.Driver.Calls);
    }

    [Fact]
    public async Task A_toggle_changed_since_the_observation_is_not_judged_by_the_old_snapshot()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        var terms = Harness.ByName(observation, "Accept terms");
        await session.PerformAsync(terms, new LocatorAction.Uncheck(), CancellationToken.None);
        await session.PerformAsync(terms, new LocatorAction.Check(), CancellationToken.None);
        Assert.Equal(["invoke chk-terms ToggleOff", "invoke chk-terms ToggleOn"], harness.Driver.Calls);
    }

    [Fact]
    public async Task A_radio_button_is_checked_by_selection_and_cannot_be_unchecked()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        var small = Harness.ByName(observation, "Small");
        await session.PerformAsync(small, new LocatorAction.Check(), CancellationToken.None);
        Assert.Equal(["invoke rad-small Select"], harness.Driver.Calls);
        var checkedTree = RawElements.Window("W", RawElements.Radio("rad-small", "Small", selected: true));
        var (other, otherSession, otherObservation) = await OpenAsync(tree: checkedTree);
        await using var __ = other;
        var error = await Assert.ThrowsAsync<EngineException>(() => otherSession.PerformAsync(Harness.ByName(otherObservation, "Small"), new LocatorAction.Uncheck(), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.NotActionable, error.Code);
    }

    [Fact]
    public async Task Select_expands_a_combo_box_reads_the_options_and_selects_the_named_one()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        var size = Harness.ByName(observation, "Size");
        harness.Driver.Faults["invoke combo-size"] = new InvalidOperationException("never");
        harness.Driver.Faults.Clear();

        // Once expanded, the options are in a popup window of the app.
        var popup = RawElements.Element("popup", "Window", null, null, new Rect(120, 592, 200, 80), RawElements.ListItem("opt-s", "Small"), RawElements.ListItem("opt-l", "Large")) with { ClassName = "Xaml_WindowedPopupClass" };
        harness.Driver.Script(session.App!.ProcessId, 0x2000, "", popup, new Rect(120, 592, 200, 80));
        await session.PerformAsync(size, new LocatorAction.Select("Large"), CancellationToken.None);
        Assert.Equal("invoke combo-size Expand", harness.Driver.Calls[0]);
        Assert.Contains("inspect 8192", harness.Driver.Calls);
        Assert.Equal("invoke opt-l Select", harness.Driver.Calls[^1]);
    }

    [Fact]
    public async Task Select_of_an_option_that_is_not_listed_names_the_ones_that_are()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(Harness.ByName(observation, "Items"), new LocatorAction.Select("Third"), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.NotActionable, error.Code);
        Assert.Contains("\"First\", \"Second\"", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Focus_and_scroll_into_view_go_to_their_patterns()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        var name = Harness.ByTestId(observation, "name-input");
        await session.PerformAsync(name, new LocatorAction.Focus(), CancellationToken.None);
        await session.PerformAsync(name, new LocatorAction.ScrollIntoView(), CancellationToken.None);
        Assert.Equal(["focus edit-name", "scrollIntoView edit-name"], harness.Driver.Calls);
    }

    [Fact]
    public async Task A_swipe_on_a_scrollable_node_scrolls_it_and_on_another_node_is_refused()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        await session.PerformAsync(Harness.ByTestId(observation, "benchmark-list"), new LocatorAction.Swipe(ScrollDirection.Down), CancellationToken.None);
        Assert.Equal([$"foreground {FakeWindowDriver.MainHandle}", "wheel 300,400 -6"], harness.Driver.Calls);
        var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(Harness.ByTestId(observation, "save-button"), new LocatorAction.Swipe(ScrollDirection.Down), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.NotActionable, error.Code);
    }

    [Fact]
    public async Task A_vertical_swipe_on_a_node_scrolls_by_pattern_when_the_desktop_cannot_take_the_wheel()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        harness.Driver.Calls.Clear();
        harness.Driver.Faults["foreground"] = new WindowDriverException(DriverFault.ForegroundLost, "locked");
        await session.PerformAsync(Harness.ByTestId(observation, "benchmark-list"), new LocatorAction.Swipe(ScrollDirection.Down), CancellationToken.None);
        Assert.Equal(["scroll list-items Down"], harness.Driver.Calls.Where(call => call.StartsWith("scroll", StringComparison.Ordinal) || call.StartsWith("wheel", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_swipe_on_an_off_screen_scrollable_node_never_turns_the_wheel_over_it()
    {
        var tree = RawElements.Window("W", RawElements.List("list-items", "Items", RawElements.ListItem("item-1", "First")) with { IsOffscreen = true });
        var (harness, session, observation) = await OpenAsync(tree: tree);
        await using var _ = harness;
        harness.Driver.Calls.Clear();
        await session.PerformAsync(Harness.ByTestId(observation, "benchmark-list"), new LocatorAction.Swipe(ScrollDirection.Down), CancellationToken.None);
        Assert.Equal(["scroll list-items Down"], harness.Driver.Calls);
    }

    [Fact]
    public async Task The_screen_root_takes_a_swipe_only()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        var root = observation.Roots[0];
        var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(root, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.NotActionable, error.Code);
        Assert.Contains("the screen root takes swipe only, not tap", error.Message, StringComparison.Ordinal);
        harness.Driver.Calls.Clear();
        await session.PerformAsync(root, new LocatorAction.Swipe(ScrollDirection.Down), CancellationToken.None);
        Assert.Equal([$"foreground {FakeWindowDriver.MainHandle}", "wheel 300,400 -6"], harness.Driver.Calls);
    }

    [Fact]
    public async Task A_viewport_swipe_scrolls_by_pattern_when_the_desktop_cannot_take_the_wheel()
    {
        var (harness, session, _) = await OpenAsync();
        await using var _ = harness;
        harness.Driver.Calls.Clear();
        harness.Driver.Faults["foreground"] = new WindowDriverException(DriverFault.ForegroundLost, "locked");
        await session.SwipeAsync(ScrollDirection.Down, CancellationToken.None);
        Assert.Equal(["scroll list-items Down"], harness.Driver.Calls.Where(call => call.StartsWith("scroll", StringComparison.Ordinal) || call.StartsWith("wheel", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_sideways_viewport_swipe_with_nothing_to_scroll_is_not_actionable_and_sends_no_input()
    {
        var (harness, session, _) = await OpenAsync(tree: RawElements.Window("Plain", RawElements.Button("b", "Go")));
        await using var __ = harness;
        harness.Driver.Calls.Clear();
        var error = await Assert.ThrowsAsync<EngineException>(() => session.SwipeAsync(ScrollDirection.Right, CancellationToken.None));
        Assert.Equal(EngineErrorCodes.NotActionable, error.Code);
        Assert.DoesNotContain(harness.Driver.Calls, call => call.StartsWith("sendKeys", StringComparison.Ordinal) || call.StartsWith("wheel", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_viewport_swipe_turns_the_wheel_when_nothing_scrolls_by_pattern()
    {
        var (harness, session, _) = await OpenAsync(tree: RawElements.Window("Plain", RawElements.Button("b", "Go")));
        await using var __ = harness;
        await session.SwipeAsync(ScrollDirection.Down, CancellationToken.None);
        Assert.Equal([$"windows {session.App!.ProcessId}", $"foreground {FakeWindowDriver.MainHandle}", "wheel 500,350 -6"], harness.Driver.Calls);
        harness.Driver.Calls.Clear();
        await session.SwipeAsync(ScrollDirection.Up, CancellationToken.None);
        Assert.Contains("wheel 500,350 6", harness.Driver.Calls);
    }

    [Fact]
    public async Task Hover_long_press_and_drag_are_pointer_moves_on_node_middles()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        var save = Harness.ByTestId(observation, "save-button");
        var list = Harness.ByTestId(observation, "benchmark-list");
        await session.HoverAsync(save);
        await session.LongPressAsync(save);
        await session.DragToAsync(save, list);
        await session.SecondaryTapAsync(save);
        Assert.Equal(
            [
                $"foreground {FakeWindowDriver.MainHandle}", "hover 540,615",
                $"foreground {FakeWindowDriver.MainHandle}", "drag 540,615 -> 540,615 hold 1000",
                $"foreground {FakeWindowDriver.MainHandle}", "drag 540,615 -> 300,400 hold 0",
                $"foreground {FakeWindowDriver.MainHandle}", "click 540,615 right",
            ],
            harness.Driver.Calls);
    }

    [Fact]
    public async Task Performs_the_locator_hover_long_press_drag_and_secondary_tap_actions_on_node_middles()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        var save = Harness.ByTestId(observation, "save-button");
        var list = Harness.ByTestId(observation, "benchmark-list");
        await session.PerformAsync(save, new LocatorAction.Hover(), CancellationToken.None);
        await session.PerformAsync(save, new LocatorAction.LongPress(), CancellationToken.None);
        await session.PerformAsync(save, new LocatorAction.DragTo(list), CancellationToken.None);
        await session.PerformAsync(save, new LocatorAction.SecondaryTap(), CancellationToken.None);
        Assert.Equal(
            [
                $"foreground {FakeWindowDriver.MainHandle}", "hover 540,615",
                $"foreground {FakeWindowDriver.MainHandle}", "drag 540,615 -> 540,615 hold 1000",
                $"foreground {FakeWindowDriver.MainHandle}", "drag 540,615 -> 300,400 hold 0",
                $"foreground {FakeWindowDriver.MainHandle}", "click 540,615 right",
            ],
            harness.Driver.Calls);
    }

    [Fact]
    public async Task Typing_goes_to_whatever_holds_focus_and_can_replace_it()
    {
        var (harness, session, _) = await OpenAsync();
        await using var __ = harness;
        await session.TypeTextAsync("hi");
        await session.TypeTextAsync("yo", replace: true);
        Assert.Equal(
            [$"foreground {FakeWindowDriver.MainHandle}", $"sendKeys {FakeWindowDriver.MainHandle} verbatim:hi", $"foreground {FakeWindowDriver.MainHandle}", $"sendKeys {FakeWindowDriver.MainHandle} ctrl+a", $"sendKeys {FakeWindowDriver.MainHandle} verbatim:yo"],
            harness.Driver.Calls);
    }

    [Theory]
    [InlineData(DriverFault.Offscreen, EngineErrorCodes.NotActionable)]
    [InlineData(DriverFault.ForegroundLost, EngineErrorCodes.NotActionable)]
    [InlineData(DriverFault.AppNotFound, EngineErrorCodes.InvalidState)]
    [InlineData(DriverFault.Other, EngineErrorCodes.EngineFailure)]
    public async Task A_driver_fault_on_an_action_carries_its_engine_code(DriverFault fault, string code)
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        harness.Driver.Faults["invoke"] = new WindowDriverException(fault, "the library said no");
        var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(Harness.ByTestId(observation, "save-button"), new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal(code, error.Code);
        Assert.Contains("perform tap failed: the library said no", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cancelled_action_is_cancelled_not_a_failure()
    {
        var (harness, session, observation) = await OpenAsync();
        await using var _ = harness;
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var error = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(Harness.ByTestId(observation, "save-button"), new LocatorAction.Tap(), cancel.Token));
        Assert.Equal(EngineErrorCodes.Cancelled, error.Code);
        Assert.Empty(harness.Driver.Calls);
    }
}
