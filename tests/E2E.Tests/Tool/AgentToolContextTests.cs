// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Tests.AgentAct;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.Tool;

/// <summary>
/// .NET-only: the second <see cref="AgentTool"/> constructor, which stands in for upstream's <c>getToolContext</c>. Upstream's tests
/// of it read a tool that observes (<c>ProjectToolDispatchTests</c>) and not the refusals.
/// </summary>
public sealed class AgentToolContextTests
{
    private static readonly JsonObject EmptySchema = new() { ["type"] = "object", ["properties"] = new JsonObject() };

    [Fact]
    public async Task Refuses_a_mutating_tool_that_asks_for_a_look_and_tells_the_model_to_look_in_a_separate_call()
    {
        var turns = 0;
        var model = new ScriptedModel(_ => turns++ == 0 ? ModelResponses.Call("change", new { }) : ModelResponses.Done("passed", "done"));
        var tools = new Dictionary<string, AgentTool>
        {
            ["change"] = new AgentTool("changes the app", EmptySchema, true, async (_, context, token) =>
            {
                await context.ObserveAsync(cancellationToken: token);
                return "changed";
            }),
        };

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("change something");
            },
            CounterPage.World(),
            model,
            agentTools: tools);

        Assert.Null(result.Error);
        Assert.Contains("only read-only tools may request observations; observe in a separate tool call", model.Requests[^1].Messages.First(message => message.Role == "tool").Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_a_tool_that_reads_its_context_when_it_runs_outside_a_step()
    {
        var tool = new AgentTool("looks", EmptySchema, false, async (_, context, token) => (await context.ObserveAsync(cancellationToken: token)).Text);

        var error = await Assert.ThrowsAsync<TestException>(() => tool.ExecuteAsync(new JsonObject(), CancellationToken.None));

        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.Contains("needs the observation context the built-in agent supplies", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_running_a_tool_built_with_the_first_constructor_as_before()
    {
        var tool = new AgentTool("echoes", EmptySchema, false, (input, _) => Task.FromResult("echo " + input.Count));

        Assert.Equal("echo 0", await tool.ExecuteAsync(new JsonObject(), CancellationToken.None));
    }
}
