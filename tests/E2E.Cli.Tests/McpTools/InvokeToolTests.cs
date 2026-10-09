// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Mcp;
using E2E.Cli.Tests.Helpers;
using static E2E.Cli.Tests.McpTools.ToolFixtures;

namespace E2E.Cli.Tests.McpTools;

public sealed class InvokeToolTests
{
    [Fact]
    public async Task Does_not_run_a_tool_whose_request_was_cancelled_while_queued()
    {
        var ran = false;
        var tool = new CatalogTool
        {
            Description = Tap.Description,
            InputSchema = Tap.InputSchema,
            Execute = (_, _) =>
            {
                ran = true;
                return Task.FromResult<object?>("never");
            },
        };

        var error = await Assert.ThrowsAsync<E2EException>(() => Tools.InvokeToolAsync("tap", tool, Json("""{ "target": "n4" }"""), new CancellationToken(canceled: true)));

        Assert.Equal("CANCELLED", error.Code);
        Assert.False(ran);
    }

    [Fact]
    public async Task Validates_the_arguments_against_the_tool_schema_and_runs_the_tool()
    {
        var result = await Tools.InvokeToolAsync("tap", Tap, Json("""{ "target": "n4" }"""), Extra);

        Assert.Equal([("text", "Tapped #n4.")], Parts(result));
        Assert.Null(result.IsError);
    }

    [Fact]
    public async Task Rejects_wrong_arguments_before_the_tool_runs_naming_the_field_and_pointing_at_tools()
    {
        var ran = false;
        var guarded = new CatalogTool
        {
            Description = Tap.Description,
            InputSchema = Tap.InputSchema,
            Execute = (_, _) =>
            {
                ran = true;
                return Task.FromResult<object?>("never");
            },
        };

        var wrong = await Assert.ThrowsAsync<ConfigurationException>(() => Tools.InvokeToolAsync("tap", guarded, Json("""{ "target": 5 }"""), Extra));
        Assert.Equal("INVALID_ARGUMENT", wrong.Code);
        Assert.Matches("""^call tap: target: .*; tools \{tool: "tap"\} shows its arguments$""", wrong.Message);
        var missing = await Assert.ThrowsAsync<ConfigurationException>(() => Tools.InvokeToolAsync("tap", guarded, Json("{}"), Extra));
        Assert.Equal("INVALID_ARGUMENT", missing.Code);
        Assert.False(ran);
    }

    [Fact]
    public async Task Refuses_an_argument_a_grammar_tool_does_not_declare_before_anything_is_dispatched()
    {
        var engine = new FakeEngine();
        await using var context = await SessionContext.StartAsync(engine);
        var catalog = SessionCatalog.Create(context, engine.Capabilities, hasSecrets: false);
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => Tools.InvokeToolAsync("tap", catalog.Find("tap")!, Json("""{ "target": "n4", "force": true }"""), Extra));

        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.Equal("call tap: Unrecognized key: \"force\"; tools {tool: \"tap\"} shows its arguments", error.Message);
        Assert.Equal(0, engine.Actions);
    }

    [Fact]
    public async Task Refuses_an_argument_the_session_catalogs_observe_and_locate_do_not_declare()
    {
        var engine = new FakeEngine();
        await using var context = await SessionContext.StartAsync(engine);
        var catalog = SessionCatalog.Create(context, engine.Capabilities, hasSecrets: false);
        var observe = await Assert.ThrowsAsync<ConfigurationException>(() => Tools.InvokeToolAsync("observe", catalog.Find("observe")!, Json("""{ "verbose": true }"""), Extra));
        Assert.Equal("INVALID_ARGUMENT", observe.Code);
        Assert.Equal("call observe: Unrecognized key: \"verbose\"; tools {tool: \"observe\"} shows its arguments", observe.Message);
        var locate = await Assert.ThrowsAsync<ConfigurationException>(() => Tools.InvokeToolAsync("locate", catalog.Find("locate")!, Json("""{ "role": "button", "selector": "#save" }"""), Extra));
        Assert.Equal("INVALID_ARGUMENT", locate.Code);
        Assert.Equal("call locate: Unrecognized key: \"selector\"; tools {tool: \"locate\"} shows its arguments", locate.Message);
        Assert.Equal(0, engine.Actions);
        Assert.Equal(0, engine.Observations);
    }

    [Fact]
    public async Task Lets_a_tool_failure_propagate_with_its_code_for_the_host_to_render()
    {
        var failing = new CatalogTool
        {
            Description = "Capture a fresh observation.",
            InputSchema = NoArguments,
            Execute = (_, _) => throw new TestException("LOCATOR_NOT_FOUND", "no observation has been captured yet"),
        };

        var error = await Assert.ThrowsAsync<TestException>(() => Tools.InvokeToolAsync("observe", failing, Json("{}"), Extra));

        Assert.Equal("LOCATOR_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Refuses_a_tool_without_an_execute_function()
    {
        var inert = new CatalogTool { Description = "x", InputSchema = NoArguments };

        var error = await Assert.ThrowsAsync<ConfigurationException>(() => Tools.InvokeToolAsync("inert", inert, Json("{}"), Extra));

        Assert.Equal("UNSUPPORTED_CAPABILITY", error.Code);
    }
}
