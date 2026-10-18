// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow.Tests.Client;

public sealed class SnapshotTests
{
    [Fact]
    public async Task Reads_the_status_once_and_then_the_tree()
    {
        var (client, agent) = DevFlow.Over();

        var snapshot = await client.CallAsync(DeviceMethods.CaptureSnapshot, "{}");
        await client.CallAsync(DeviceMethods.CaptureSnapshot, "{}");

        Assert.Equal(["GET /api/v1/agent/status", "GET /api/v1/ui/tree", "GET /api/v1/ui/tree"], agent.Calls.Select(call => call.Route));
        Assert.Equal("true", agent.Sent("GET /api/v1/ui/tree").First().Query("envelope"));
        Assert.Equal("MobileApp", snapshot!["appName"]!.GetValue<string>());
        Assert.Equal(FakeAgent.PackageId, snapshot["appBundleId"]!.GetValue<string>());
        Json.AssertEqual("""{"width":390,"height":844}""", snapshot["viewport"]);
        Assert.False(snapshot["truncated"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Flattens_the_tree_with_its_coordinates_and_maps_the_controls_onto_agent_device_kinds()
    {
        var (client, _) = DevFlow.Over();

        var nodes = DevFlow.Nodes(await client.CallAsync(DeviceMethods.CaptureSnapshot, "{}"));

        Assert.Equal(14, nodes.Count);
        Json.AssertEqual(
            """{"ref":"win_0","index":0,"depth":0,"type":"window","kind":"window","label":"MobileApp","rect":{"x":0,"y":0,"width":390,"height":844},"enabled":true,"focused":true,"selected":false,"visibleToUser":true,"hittable":true}""",
            nodes["win_0"]);
        Assert.Equal("content-page", nodes["page_main"]["type"]!.GetValue<string>());
        Assert.Equal(0, nodes["page_main"]["parentIndex"]!.GetValue<int>());
        Assert.Equal(1, nodes["page_main"]["depth"]!.GetValue<int>());
        Assert.Equal("vertical-stack-layout", nodes["stack_0"]["type"]!.GetValue<string>());
        Assert.Equal(3, nodes["lbl_title"]["parentIndex"]!.GetValue<int>());
        Assert.Equal(4, nodes["lbl_title"]["depth"]!.GetValue<int>());

        // A label is static text; one the agent calls a heading or a link is that.
        Assert.Equal("heading", nodes["lbl_title"]["type"]!.GetValue<string>());
        Assert.True(nodes["lbl_title"]["heading"]!.GetValue<bool>());
        Assert.Equal("static-text", nodes["lbl_sub"]["type"]!.GetValue<string>());
        Assert.Equal("Type a name and the app greets you.", nodes["lbl_sub"]["label"]!.GetValue<string>());
        Assert.Equal("link", nodes["lbl_link"]["type"]!.GetValue<string>());

        Json.AssertEqual(
            """{"ref":"btn_greet","index":7,"depth":4,"type":"button","kind":"button","parentIndex":3,"identifier":"greet","label":"Greet","rect":{"x":24,"y":261,"width":342,"height":44},"enabled":true,"focused":false,"selected":false,"visibleToUser":true,"hittable":true}""",
            nodes["btn_greet"]);
    }

    [Fact]
    public async Task A_field_reports_its_text_as_its_value_and_a_password_field_reports_no_value()
    {
        var (client, _) = DevFlow.Over();

        var nodes = DevFlow.Nodes(await client.CallAsync(DeviceMethods.CaptureSnapshot, "{}"));

        var name = nodes["entry_name"];
        Assert.Equal("text-field", name["type"]!.GetValue<string>());
        Assert.Equal("name", name["identifier"]!.GetValue<string>());
        Assert.Equal("", name["value"]!.GetValue<string>());
        Assert.Null(name["label"]);
        Assert.True(name["editable"]!.GetValue<bool>());
        Assert.True(name["focused"]!.GetValue<bool>());
        Assert.Equal("Ada Lovelace", name["placeholder"]!.GetValue<string>());

        var secret = nodes["entry_secret"];
        Assert.True(secret["password"]!.GetValue<bool>());
        Assert.Null(secret["value"]);
        Assert.DoesNotContain(ElementTree.Redacted, secret.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_toggle_reports_its_state_as_checked_and_a_hidden_label_as_not_visible()
    {
        var (client, _) = DevFlow.Over();

        var nodes = DevFlow.Nodes(await client.CallAsync(DeviceMethods.CaptureSnapshot, "{}"));

        var toggle = nodes["sw_dark"];
        Assert.Equal("switch", toggle["type"]!.GetValue<string>());
        Assert.True(toggle["checked"]!.GetValue<bool>());
        Assert.Null(toggle["value"]);
        Assert.False(toggle["enabled"]!.GetValue<bool>());
        Assert.False(toggle["hittable"]!.GetValue<bool>());

        var error = nodes["lbl_error"];
        Assert.False(error["visibleToUser"]!.GetValue<bool>());
        Assert.False(error["hittable"]!.GetValue<bool>());
        Assert.Equal("Enter a name first.", error["label"]!.GetValue<string>());
    }

    [Fact]
    public async Task An_interactive_snapshot_keeps_the_controls_and_the_labelled_or_identified_nodes_and_reparents_them()
    {
        var (client, _) = DevFlow.Over();

        var nodes = DevFlow.Nodes(await client.CallAsync(DeviceMethods.CaptureSnapshot, """{"interactiveOnly":true}"""));

        Assert.Equal(["win_0", "lbl_title", "lbl_sub", "entry_name", "btn_greet", "lbl_error", "lbl_counter", "btn_increment", "lbl_link", "sw_dark", "entry_secret"], nodes.Keys);
        Assert.Equal(0, nodes["btn_greet"]["parentIndex"]!.GetValue<int>());
        Assert.Equal(1, nodes["btn_greet"]["depth"]!.GetValue<int>());
        Assert.Equal(4, nodes["btn_greet"]["index"]!.GetValue<int>());
    }

    [Fact]
    public async Task Reads_a_bare_element_array_as_well_as_the_envelope()
    {
        var (client, agent) = DevFlow.Over();
        agent.ScriptJson("GET", "/api/v1/ui/tree", FakeAgent.Tree());

        var snapshot = await client.CallAsync(DeviceMethods.CaptureSnapshot, "{}");

        Assert.Equal(14, snapshot!["nodes"]!.AsArray().Count);
    }

    [Fact]
    public async Task A_tree_the_agent_cannot_give_is_an_unsupported_capability()
    {
        var (client, agent) = DevFlow.Over();
        agent.ScriptJson("GET", "/api/v1/ui/tree", new JsonObject { ["error"] = "not_supported", ["capability"] = "ui.tree", ["reason"] = "Not supported by the 'native' DevFlow backend." }, System.Net.HttpStatusCode.NotImplemented);

        var error = await client.FailureAsync(DeviceMethods.CaptureSnapshot, "{}");

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.Equal("UNSUPPORTED_CAPABILITY", DevFlow.EngineCode(error));
    }
}
