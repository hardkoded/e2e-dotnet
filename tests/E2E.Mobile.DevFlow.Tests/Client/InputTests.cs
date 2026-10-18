// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow.Tests.Client;

public sealed class InputTests
{
    [Fact]
    public async Task Taps_a_node_by_its_element_id_with_the_capture_epoch_and_the_lease()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsPress, """{"ref":"@btn_greet","settle":true,"settleQuietMs":150}""");

        var call = Assert.Single(agent.Calls);
        Assert.Equal("POST /api/v1/ui/actions/tap", call.Route);
        Json.AssertEqual("""{"elementId":"btn_greet","captureEpoch":7}""", call.Body);
        Assert.Equal("lease-1", call.Header("X-DevFlow-Lease"));
        Assert.Equal("e2e", call.Header("X-DevFlow-Holder"));
        Assert.Equal("e2e mobileapp slot 1 of 1", call.Header("X-DevFlow-Label"));
    }

    [Fact]
    public async Task Every_mutation_claims_the_lease_first_since_the_agent_lets_it_lapse_after_ten_seconds()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        agent.LeaseCalls.Clear();

        await client.CallAsync(DeviceMethods.InteractionsPress, """{"ref":"@btn_greet"}""");
        await client.CallAsync(DeviceMethods.InteractionsFill, """{"ref":"@entry_name","text":"Grace"}""");
        await client.CallAsync(DeviceMethods.CaptureSnapshot, "{}");

        Assert.Equal(2, agent.LeaseCalls.Count);
        Json.AssertEqual("""{"action":"claim","leaseId":"lease-1","holderKind":"e2e","label":"e2e mobileapp slot 1 of 1"}""", agent.LeaseCalls[0].Body);
        Assert.Equal(["POST /api/v1/ui/actions/tap", "POST /api/v1/ui/actions/fill", "GET /api/v1/ui/tree"], agent.Calls.Select(call => call.Route));
    }

    [Fact]
    public async Task A_lease_another_host_holds_stops_the_action_before_it_is_sent()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        agent.ScriptLeaseHeldBy("inspector", "VS Code");

        var failure = await client.FailureAsync(DeviceMethods.InteractionsPress, """{"ref":"@btn_greet"}""");

        Assert.Empty(agent.Calls);
        Assert.Equal("COMMAND_FAILED", failure.Code);
        Assert.Contains("inspector (VS Code)", failure.Message, StringComparison.Ordinal);
        Assert.Equal("inspector", failure.Details!["holderKind"]);
    }

    [Fact]
    public async Task A_ref_the_newest_screen_does_not_have_is_stale_before_anything_is_sent()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        var error = await client.FailureAsync(DeviceMethods.InteractionsPress, """{"ref":"@gone"}""");

        Assert.Empty(agent.Calls);
        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Equal(DevFlowErrors.StaleRef, error.Details!["reason"]);
        Assert.Equal("NODE_STALE", DevFlow.ActionCode(error));
    }

    [Fact]
    public async Task A_double_tap_is_the_agents_gesture_and_a_tap_at_a_point_is_a_tap_there()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsPress, """{"ref":"@btn_greet","count":2}""");
        await client.CallAsync(DeviceMethods.InteractionsPress, """{"x":100,"y":270.5}""");

        // The agent's tap takes an element, so a point is hit-tested to the topmost element there first.
        Assert.Equal(["POST /api/v1/ui/actions/gesture", "GET /api/v1/ui/hit-test", "POST /api/v1/ui/actions/tap"], agent.Calls.Select(call => call.Route));
        Json.AssertEqual("""{"elementId":"btn_greet","captureEpoch":7,"type":"doubletap"}""", agent.Calls[0].Body);
        Assert.Equal(("100", "270.5"), (agent.Calls[1].Query("x"), agent.Calls[1].Query("y")));
        Json.AssertEqual("""{"elementId":"btn_greet","captureEpoch":7}""", agent.Calls[2].Body);
    }

    [Fact]
    public async Task A_hit_test_the_screen_changed_under_is_tried_again()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        var changing = 1;
        agent.Script("GET", "/api/v1/ui/hit-test", _ => changing-- > 0
            ? FakeAgent.Failure(HttpStatusCode.Conflict, "The UI changed while it was being captured. Retry the tree, query, or hit-test request.", "capture-changed-during-read")
            : FakeAgent.Json(new JsonObject { ["x"] = 100, ["y"] = 270.5, ["captureEpoch"] = 7, ["elements"] = new JsonArray(new JsonObject { ["id"] = "btn_greet" }) }));

        await client.CallAsync(DeviceMethods.InteractionsPress, """{"x":100,"y":270.5}""");

        Assert.Equal(2, agent.Sent("GET /api/v1/ui/hit-test").Count());
        Assert.Single(agent.Sent("POST /api/v1/ui/actions/tap"));
    }

    [Fact]
    public async Task A_tap_at_a_point_with_nothing_under_it_fails()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        agent.ScriptJson("GET", "/api/v1/ui/hit-test", new JsonObject { ["x"] = 1, ["y"] = 1, ["captureEpoch"] = 7, ["elements"] = new JsonArray() });

        var failure = await client.FailureAsync(DeviceMethods.InteractionsPress, """{"x":1,"y":1}""");

        Assert.Equal("COMMAND_FAILED", failure.Code);
        Assert.Single(agent.Calls);
    }

    [Fact]
    public async Task A_long_press_holds_a_second_unless_told_otherwise()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsLongPress, """{"ref":"@lbl_link"}""");
        await client.CallAsync(DeviceMethods.InteractionsLongPress, """{"ref":"@lbl_link","durationMs":1500}""");

        Json.AssertEqual("""{"elementId":"lbl_link","captureEpoch":7,"type":"longpress","durationMs":1000}""", agent.Calls[0].Body);
        Json.AssertEqual("""{"elementId":"lbl_link","captureEpoch":7,"type":"longpress","durationMs":1500}""", agent.Calls[1].Body);
    }

    [Fact]
    public async Task Fills_a_field_by_id_and_clears_it_with_an_empty_text()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsFill, """{"ref":"@entry_name","text":"Grace"}""");
        await client.CallAsync(DeviceMethods.InteractionsFill, """{"ref":"@entry_name","text":""}""");

        Assert.Equal(["POST /api/v1/ui/actions/fill", "POST /api/v1/ui/actions/clear"], agent.Calls.Select(call => call.Route));
        Json.AssertEqual("""{"elementId":"entry_name","captureEpoch":7,"text":"Grace"}""", agent.Calls[0].Body);
        Json.AssertEqual("""{"elementId":"entry_name","captureEpoch":7}""", agent.Calls[1].Body);
    }

    [Fact]
    public async Task Types_into_the_field_that_has_the_focus_after_a_fresh_read()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsType, """{"text":"Ada"}""");
        await client.CallAsync(DeviceMethods.InteractionsType, """{"text":"delete"}""");

        // The key is a word that names no key, so the agent appends the text instead of reading "delete" as the key it also is.
        Assert.Equal(["GET /api/v1/ui/tree", "POST /api/v1/ui/actions/key", "GET /api/v1/ui/tree", "POST /api/v1/ui/actions/key"], agent.Calls.Select(call => call.Route));
        Json.AssertEqual("""{"elementId":"entry_name","captureEpoch":7,"key":"text","text":"Ada"}""", agent.Calls[1].Body);
        Json.AssertEqual("""{"elementId":"entry_name","captureEpoch":7,"key":"text","text":"delete"}""", agent.Calls[3].Body);
    }

    [Fact]
    public async Task Typing_with_no_focused_field_is_refused_without_sending_keys()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        agent.ScriptJson("GET", "/api/v1/ui/tree", FakeAgent.Envelope(FakeAgent.Tree(nameFocused: false)));

        var error = await client.FailureAsync(DeviceMethods.InteractionsType, """{"text":"Ada"}""");

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.DoesNotContain(agent.Calls, call => call.Route.EndsWith("/key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Enter_completes_the_focused_field_and_dismiss_has_no_agent_equivalent()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        await client.CallAsync(DeviceMethods.CommandKeyboard, """{"action":"enter"}""");
        var error = await client.FailureAsync(DeviceMethods.CommandKeyboard, """{"action":"dismiss"}""");

        Json.AssertEqual("""{"elementId":"entry_name","captureEpoch":7,"key":"Enter"}""", agent.Sent("POST /api/v1/ui/actions/key").Single().Body);
        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
    }

    [Fact]
    public async Task A_screen_scroll_moves_the_finger_the_other_way_across_half_the_viewport()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsScroll, """{"direction":"down","settle":true}""");
        await client.CallAsync(DeviceMethods.InteractionsScroll, """{"direction":"left","amount":0.25}""");

        Json.AssertEqual("""{"type":"swipe","direction":"up","distance":422,"durationMs":200}""", agent.Calls[0].Body);
        Json.AssertEqual("""{"type":"swipe","direction":"right","distance":97.5,"durationMs":200}""", agent.Calls[1].Body);
    }

    [Fact]
    public async Task A_scroll_on_a_node_swipes_inside_it()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsScroll, """{"ref":"@scroll_0","direction":"down","amount":0.5}""");

        Json.AssertEqual("""{"elementId":"scroll_0","captureEpoch":7,"type":"swipe","direction":"up","distance":375.5,"durationMs":200}""", Assert.Single(agent.Calls).Body);
    }

    [Fact]
    public async Task A_swipe_between_two_points_becomes_a_direction_and_a_distance()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsSwipe, """{"from":{"x":200,"y":600},"to":{"x":200,"y":300}}""");

        Json.AssertEqual("""{"type":"swipe","direction":"up","distance":300,"durationMs":200}""", Assert.Single(agent.Calls).Body);
    }

    [Fact]
    public async Task A_pan_from_a_node_carries_its_deltas()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        await client.CallAsync(DeviceMethods.InteractionsPan, """{"ref":"@scroll_0","dx":-40,"dy":120,"durationMs":500}""");

        Json.AssertEqual("""{"elementId":"scroll_0","captureEpoch":7,"type":"pan","deltaX":-40,"deltaY":120,"durationMs":500}""", Assert.Single(agent.Calls).Body);
    }

    [Fact]
    public async Task Back_pops_the_navigation()
    {
        var (client, agent) = await DevFlow.ReadAsync();

        await client.CallAsync(DeviceMethods.CommandBack, """{"settle":true,"settleQuietMs":150}""");

        Assert.Equal("POST /api/v1/ui/actions/back", Assert.Single(agent.Calls).Route);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "The UI snapshot is stale. Capture the tree or hit-test again before acting.", "stale-capture-epoch")]
    [InlineData(HttpStatusCode.BadRequest, "Element not found", null)]
    [InlineData(HttpStatusCode.NotFound, "No element found with ID 'btn_greet'.", "element-not-found")]
    public async Task A_target_the_agent_no_longer_has_is_a_stale_node(HttpStatusCode status, string error, string? reason)
    {
        var (client, agent) = await DevFlow.ReadAsync();
        agent.ScriptFailure("POST", "/api/v1/ui/actions/tap", status, error, reason);

        var failure = await client.FailureAsync(DeviceMethods.InteractionsPress, """{"ref":"@btn_greet"}""");

        Assert.Equal("COMMAND_FAILED", failure.Code);
        Assert.Equal(DevFlowErrors.StaleRef, failure.Details!["reason"]);
        Assert.Equal("NODE_STALE", DevFlow.ActionCode(failure));
    }

    [Fact]
    public async Task An_action_the_agent_refuses_for_another_reason_fails_the_command()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        agent.ScriptFailure("POST", "/api/v1/ui/actions/fill", HttpStatusCode.BadRequest, "Element 'btn_greet' does not accept text input");

        var failure = await client.FailureAsync(DeviceMethods.InteractionsFill, """{"ref":"@btn_greet","text":"x"}""");

        Assert.Equal("COMMAND_FAILED", failure.Code);
        Assert.False(failure.Details!.ContainsKey("reason"));
        Assert.Contains("does not accept text input", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_action_this_agent_does_not_serve_is_an_unsupported_capability()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        agent.ScriptFailure("POST", "/api/v1/ui/actions/gesture", HttpStatusCode.NotImplemented, "Not supported by the 'android-views' DevFlow backend.", "not_supported");

        var failure = await client.FailureAsync(DeviceMethods.InteractionsLongPress, """{"ref":"@lbl_link"}""");

        Assert.Equal("UNSUPPORTED_OPERATION", failure.Code);
        Assert.Equal("UNSUPPORTED_CAPABILITY", DevFlow.EngineCode(failure));
    }

    [Fact]
    public async Task A_mutation_the_agent_refuses_for_the_lease_names_the_holder_in_the_failure()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        agent.ScriptFailure(
            "POST",
            "/api/v1/ui/actions/tap",
            HttpStatusCode.Conflict,
            "Another DevFlow session is driving this app. Take control before mutating it.",
            "lease",
            new JsonObject { ["HolderKind"] = "mcp", ["Label"] = "Copilot", ["ExpiresInMs"] = 8000 });

        var failure = await client.FailureAsync(DeviceMethods.InteractionsPress, """{"ref":"@btn_greet"}""");

        Assert.Equal("COMMAND_FAILED", failure.Code);
        Assert.False(failure.Details!.ContainsKey("reason"));
        Assert.Contains("mutation lease: mcp (Copilot)", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_mutation_refused_for_a_lease_nobody_holds_says_the_lease_lapsed()
    {
        var (client, agent) = await DevFlow.ReadAsync();
        agent.ScriptFailure("POST", "/api/v1/ui/actions/tap", HttpStatusCode.Conflict, "Another DevFlow session is driving this app.", "lease");

        var failure = await client.FailureAsync(DeviceMethods.InteractionsPress, """{"ref":"@btn_greet"}""");

        Assert.Contains("lapsed", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DeviceMethods.InteractionsLongPress, """{"x":10,"y":10}""")]
    [InlineData(DeviceMethods.InteractionsPan, """{"x":10,"y":10,"dx":5,"dy":5}""")]
    [InlineData(DeviceMethods.InteractionsPress, """{"x":10,"y":10,"count":2}""")]
    [InlineData(DeviceMethods.InteractionsDrag, """{"source":"@btn_greet","destination":"@btn_increment"}""")]
    [InlineData(DeviceMethods.InteractionsHover, """{"ref":"@btn_greet"}""")]
    public async Task Refuses_the_gestures_the_agent_has_no_equivalent_for_without_sending_anything(string method, string options)
    {
        var (client, agent) = await DevFlow.ReadAsync();

        var error = await client.FailureAsync(method, options);

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.Empty(agent.Calls);
    }
}
