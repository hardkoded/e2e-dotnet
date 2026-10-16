// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.TestMu.Mobile.Tests.Client;

public sealed class InputTests
{
    private const string TapOnSave = "move 125,220 (0ms) | down | pause 50 | up";

    [Fact]
    public async Task Taps_the_centre_of_the_node_a_ref_names()
    {
        var (client, hub, _) = await Appium.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsPress, """{"ref":"@e3","settle":true,"settleQuietMs":300}""");

        var call = Assert.Single(hub.Calls);
        Assert.Equal("POST actions", $"{call.Method} {call.Command}");
        Assert.Equal(TapOnSave, Gesture.Describe(call.Body));
    }

    [Fact]
    public async Task Taps_a_point()
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.InteractionsPress, """{"x":10.4,"y":20.6}""");

        Assert.Equal("move 10,21 (0ms) | down | pause 50 | up", Gesture.Describe(Assert.Single(hub.Calls).Body));
    }

    [Fact]
    public async Task A_double_tap_is_two_taps_in_one_sequence()
    {
        var (client, hub, _) = await Appium.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsPress, """{"ref":"@e3","count":2}""");

        Assert.Equal($"{TapOnSave} | pause 100 | down | pause 50 | up", Gesture.Describe(Assert.Single(hub.Calls).Body));
    }

    [Fact]
    public async Task Holds_a_long_press_for_its_duration()
    {
        var (client, hub, _) = await Appium.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsLongPress, """{"ref":"@e3","durationMs":1500}""");

        Assert.Equal("move 125,220 (0ms) | down | pause 1500 | up", Gesture.Describe(Assert.Single(hub.Calls).Body));
    }

    [Fact]
    public async Task Swipes_between_two_points()
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.InteractionsSwipe, """{"from":{"x":200,"y":600},"to":{"x":200,"y":200}}""");

        Assert.Equal("move 200,600 (0ms) | down | move 200,200 (300ms) | up", Gesture.Describe(Assert.Single(hub.Calls).Body));
    }

    [Fact]
    public async Task Pans_by_a_delta_over_its_duration()
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.InteractionsPan, """{"x":100,"y":100,"dx":50,"dy":-20,"durationMs":800}""");

        Assert.Equal("move 100,100 (0ms) | down | move 150,80 (800ms) | up", Gesture.Describe(Assert.Single(hub.Calls).Body));
    }

    [Fact]
    public async Task Drags_from_the_centre_of_one_node_to_the_centre_of_another_after_a_hold()
    {
        var (client, hub, _) = await Appium.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsDrag, """{"source":"@e3","destination":"@e4"}""");

        Assert.Equal("move 125,220 (0ms) | down | pause 500 | move 120,320 (500ms) | up", Gesture.Describe(Assert.Single(hub.Calls).Body));
    }

    [Theory]
    [InlineData("down", "move 195,633 (0ms) | down | move 195,211 (300ms) | up")]
    [InlineData("up", "move 195,211 (0ms) | down | move 195,633 (300ms) | up")]
    [InlineData("right", "move 293,422 (0ms) | down | move 98,422 (300ms) | up")]
    [InlineData("left", "move 98,422 (0ms) | down | move 293,422 (300ms) | up")]
    public async Task Scrolls_the_content_by_moving_the_finger_the_other_way(string direction, string expected)
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.InteractionsScroll, $$"""{"direction":"{{direction}}"}""");

        Assert.Equal(["GET window/rect", "POST actions"], hub.Calls.Select(call => $"{call.Method} {call.Command}"));
        Assert.Equal(expected, Gesture.Describe(hub.Calls[1].Body));
    }

    [Fact]
    public async Task A_scroll_amount_is_a_share_of_the_screen()
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.InteractionsScroll, """{"direction":"down","amount":0.25}""");

        Assert.Equal("move 195,528 (0ms) | down | move 195,317 (300ms) | up", Gesture.Describe(hub.Calls[1].Body));
    }

    [Fact]
    public async Task Fills_a_field_by_tapping_it_then_clearing_and_typing_into_the_focused_element()
    {
        var (client, hub, _) = await Appium.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsFill, """{"ref":"@e4","text":"hello"}""");

        Assert.Equal(["POST actions", "GET element/active", "POST element/el-1/clear", "POST element/el-1/value"], hub.Calls.Select(call => $"{call.Method} {call.Command}"));
        Assert.Equal("move 120,320 (0ms) | down | pause 50 | up", Gesture.Describe(hub.Calls[0].Body));
        Json.AssertEqual("""{"text":"hello","value":["h","e","l","l","o"]}""", hub.Calls[3].Body);
    }

    [Fact]
    public async Task Filling_with_no_text_only_clears()
    {
        var (client, hub, _) = await Appium.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsFill, """{"ref":"@e4","text":""}""");

        Assert.Equal(["POST actions", "GET element/active", "POST element/el-1/clear"], hub.Calls.Select(call => $"{call.Method} {call.Command}"));
    }

    [Fact]
    public async Task Filling_when_the_tap_focuses_nothing_fails_without_typing()
    {
        var (client, hub, _) = await Appium.ReadAsync();
        hub.Script("GET", "element/active", _ => FakeAppium.Failure(System.Net.HttpStatusCode.NotFound, "no such element", "no active element"));

        var error = await client.FailureAsync(DeviceMethods.InteractionsFill, """{"ref":"@e4","text":"hello"}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Empty(hub.Sent("element/el-1/value"));
    }

    [Fact]
    public async Task Types_into_the_focused_element()
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.InteractionsType, """{"text":"ab"}""");

        Assert.Equal(["GET element/active", "POST element/el-1/value"], hub.Calls.Select(call => $"{call.Method} {call.Command}"));
        Json.AssertEqual("""{"text":"ab","value":["a","b"]}""", hub.Calls[1].Body);
    }

    [Fact]
    public async Task Types_as_key_presses_when_no_element_has_the_focus()
    {
        var (client, hub, _) = Appium.Over();
        hub.Script("GET", "element/active", _ => FakeAppium.Failure(System.Net.HttpStatusCode.NotFound, "no such element", "none"));

        await client.CallAsync(DeviceMethods.InteractionsType, """{"text":"ab"}""");

        Assert.Equal("key a | release | key b | release", Gesture.Describe(hub.Calls[1].Body));
    }

    [Fact]
    public async Task Enter_is_the_Enter_key_and_dismiss_hides_the_keyboard()
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.CommandKeyboard, """{"action":"enter"}""");
        await client.CallAsync(DeviceMethods.CommandKeyboard, """{"action":"dismiss"}""");

        Assert.Equal("key U+E007 | release", Gesture.Describe(hub.Calls[0].Body));
        Assert.Equal("mobile: hideKeyboard", hub.Calls[1].Script);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("dismiss")]
    public async Task Answers_an_alert(string action)
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.CommandAlert, $$"""{"action":"{{action}}"}""");

        var call = Assert.Single(hub.Calls);
        Assert.Equal($"POST alert/{action}", $"{call.Method} {call.Command}");
    }

    [Fact]
    public async Task Home_presses_the_home_button_on_iOS_and_the_home_key_on_Android()
    {
        var (ios, iosHub, _) = Appium.Over(MobilePlatform.Ios);
        var (android, androidHub, _) = Appium.Over(MobilePlatform.Android);

        await ios.CallAsync(DeviceMethods.CommandHome);
        await android.CallAsync(DeviceMethods.CommandHome);

        var pressed = Assert.Single(iosHub.Calls);
        Assert.Equal("mobile: pressButton", pressed.Script);
        Json.AssertEqual("""{"name":"home"}""", pressed.Arguments);
        var key = Assert.Single(androidHub.Calls);
        Assert.Equal("mobile: pressKey", key.Script);
        Json.AssertEqual("""{"keycode":3}""", key.Arguments);
    }

    [Fact]
    public async Task Back_on_Android_is_the_system_back()
    {
        var (client, hub, _) = Appium.Over(MobilePlatform.Android);

        await client.CallAsync(DeviceMethods.CommandBack, """{"settle":true}""");

        var call = Assert.Single(hub.Calls);
        Assert.Equal("POST back", $"{call.Method} {call.Command}");
    }

    [Fact]
    public async Task Back_on_iOS_reads_the_screen_and_taps_the_back_button_of_the_navigation_bar()
    {
        var (client, hub, converter) = Appium.Over();

        await client.CallAsync(DeviceMethods.CommandBack);

        Assert.Equal(["GET source", "POST actions"], hub.Calls.Select(call => $"{call.Method} {call.Command}"));
        Assert.Equal("move 40,70 (0ms) | down | pause 50 | up", Gesture.Describe(hub.Calls[1].Body));
        Assert.Single(converter.Sources);
    }

    [Fact]
    public async Task Back_on_iOS_is_unsupported_when_the_screen_has_no_navigation_bar()
    {
        var (client, hub, _) = Appium.Over(screen: (JsonObject)JsonNode.Parse("""{"nodes":[{"ref":"e1","index":0,"type":"Button","rect":{"x":0,"y":0,"width":10,"height":10}}]}""")!);

        var error = await client.FailureAsync(DeviceMethods.CommandBack);

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.Empty(hub.Sent("actions"));
    }

    [Theory]
    [InlineData("portrait", "PORTRAIT")]
    [InlineData("landscape-left", "LANDSCAPE")]
    [InlineData("landscape-right", "LANDSCAPE")]
    public async Task Turns_the_screen(string orientation, string expected)
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.CommandOrientation, $$"""{"orientation":"{{orientation}}"}""");

        var call = Assert.Single(hub.Calls);
        Assert.Equal("POST orientation", $"{call.Method} {call.Command}");
        Json.AssertEqual($$"""{"orientation":"{{expected}}"}""", call.Body);
    }

    [Fact]
    public async Task Upside_down_is_unsupported()
    {
        var (client, hub, _) = Appium.Over();

        var error = await client.FailureAsync(DeviceMethods.CommandOrientation, """{"orientation":"portrait-upside-down"}""");

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.Empty(hub.Calls);
    }

    [Fact]
    public async Task Reads_and_writes_the_pasteboard_on_iOS()
    {
        var (client, hub, _) = Appium.Over();
        hub.ScriptValue("POST", "execute/sync", Convert.ToBase64String("copied"u8.ToArray()));

        await client.CallAsync(DeviceMethods.CommandClipboard, """{"action":"write","text":"héllo"}""");
        var read = await client.CallAsync(DeviceMethods.CommandClipboard, """{"action":"read"}""");

        Assert.Equal("mobile: setPasteboard", hub.Calls[0].Script);
        Json.AssertEqual($$"""{"content":"{{Convert.ToBase64String("héllo"u8.ToArray())}}","encoding":"utf8"}""", hub.Calls[0].Arguments);
        Assert.Equal("mobile: getPasteboard", hub.Calls[1].Script);
        Json.AssertEqual("""{"action":"read","text":"copied"}""", read);
    }

    [Fact]
    public async Task Reads_and_writes_the_clipboard_on_Android()
    {
        var (client, hub, _) = Appium.Over(MobilePlatform.Android);
        hub.ScriptValue("POST", "execute/sync", Convert.ToBase64String("copied"u8.ToArray()));

        await client.CallAsync(DeviceMethods.CommandClipboard, """{"action":"write","text":"abc"}""");
        var read = await client.CallAsync(DeviceMethods.CommandClipboard, """{"action":"read"}""");

        Assert.Equal("mobile: setClipboard", hub.Calls[0].Script);
        Json.AssertEqual($$"""{"content":"{{Convert.ToBase64String("abc"u8.ToArray())}}","contentType":"plaintext"}""", hub.Calls[0].Arguments);
        Assert.Equal("mobile: getClipboard", hub.Calls[1].Script);
        Json.AssertEqual("""{"action":"read","text":"copied"}""", read);
    }

    [Fact]
    public async Task A_clipboard_that_is_not_base64_fails()
    {
        var (client, hub, _) = Appium.Over();
        hub.ScriptValue("POST", "execute/sync", "not base64!");

        var error = await client.FailureAsync(DeviceMethods.CommandClipboard, """{"action":"read"}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
    }
}
