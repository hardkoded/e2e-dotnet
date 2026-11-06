// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Tests.CoreTests;
using static E2E.Tests.ToolLoopToolChoice.FakeLoopModel;

namespace E2E.Tests.ToolLoopToolChoice;

/// <summary>
/// The "tool loop forced tool choice" describe. The port ends a step with a <c>done</c> tool, not <c>complete_step</c>, so the tests keep
/// upstream's names and assert <c>done</c> where upstream asserts <c>complete_step</c>.
/// </summary>
public sealed class ToolLoopForcedToolChoiceTests
{
    private static DocumentWorld Page() => new DocumentWorld().Map("/start", page => page.Button("Peek"));

    private static ModelResponse Done() => ModelResponses.Done("passed", "done");

    [Fact]
    public async Task Retries_with_auto_under_the_same_instructions_when_the_model_rejects_a_forced_choice()
    {
        var loop = new FakeLoopModel(RefusingForcedChoice(call => call.Turn == 2 ? ModelResponses.Call("peek", new { }) : Done()));
        ActResult? result = null;

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                result = await ctx.Agent.ActAsync("look around");
            },
            Page(),
            loop.Model,
            agentTools: Peek());

        Assert.Null(attempt.Error);
        Assert.Equal(["required", "auto", "auto"], loop.Calls.Select(call => call.ToolChoice));
        // The retried request is the refused one, sent again: the tool-calls-only rule was in the instructions from the start, so the
        // switch changes nothing a provider binds reasoning to.
        Assert.Equal(loop.Calls[0].Prompt, loop.Calls[1].Prompt);
        Assert.Contains("Reply with tool calls only", loop.Calls[0].System, StringComparison.Ordinal);
        Assert.All(loop.Calls, call => Assert.Equal(loop.Calls[0].System, call.System));
        // The refused request never answered, so it is not a model call the step paid for.
        Assert.Equal(2, result!.ModelCalls);
    }

    [Fact]
    public async Task Remembers_the_refusal_for_later_steps_on_the_same_model()
    {
        var loop = new FakeLoopModel(RefusingForcedChoice(_ => Done()));

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                await ctx.Agent.ActAsync("first");
                await ctx.Agent.ActAsync("second");
            },
            Page(),
            loop.Model);

        Assert.Null(attempt.Error);
        Assert.Equal(["required", "auto", "auto"], loop.Calls.Select(call => call.ToolChoice));
    }

    [Fact]
    public async Task Offers_only_complete_step_under_auto_on_the_forced_conclusion_turns()
    {
        var loop = new FakeLoopModel(RefusingForcedChoice(call =>
            call.ToolNames is ["done"] ? Done() : ModelResponses.Call("peek", new { })));

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                await ctx.Agent.ActAsync("look until told to stop", new ActOptions { MaxModelCalls = 4 });
            },
            Page(),
            loop.Model,
            agentTools: Peek());

        Assert.Null(attempt.Error);
        Assert.Equal(["done"], loop.Calls[^1].ToolNames);
        Assert.Equal("auto", loop.Calls[^1].ToolChoice);
    }

    [Fact]
    public async Task Asks_again_when_a_turn_comes_back_as_prose_without_a_tool_call()
    {
        var loop = new FakeLoopModel(RefusingForcedChoice(call => call.Turn == 2 ? new ModelResponse { Content = "I will look around first." } : Done()));
        ActResult? result = null;

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                result = await ctx.Agent.ActAsync("do the thing");
            },
            Page(),
            loop.Model);

        Assert.Null(attempt.Error);
        Assert.Equal(3, loop.Calls.Count);
        Assert.Contains("Your last reply had no tool call", loop.Calls[2].LastPrompt, StringComparison.Ordinal);
        Assert.Equal(loop.Calls[1].UserMessages + 1, loop.Calls[2].UserMessages);
        // The refused request is not a model call; the prose turn is one the step paid for.
        Assert.Equal(2, result!.ModelCalls);
    }

    [Fact]
    public async Task Fails_with_STEP_NO_CONCLUSION_when_prose_replies_use_up_the_turns()
    {
        var loop = new FakeLoopModel(RefusingForcedChoice(_ => new ModelResponse { Content = "Thinking about it." }));

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                await ctx.Agent.ActAsync("do the thing", new ActOptions { MaxModelCalls = 3 });
            },
            Page(),
            loop.Model);

        Assert.Equal("STEP_NO_CONCLUSION", Assert.IsType<AgentException>(attempt.Error).Code);
        // One refusal, then three prose turns: the budget, not the loop, ends it.
        Assert.Equal(4, loop.Calls.Count);
    }

    [Fact]
    public async Task Leaves_a_400_about_anything_else_to_the_ordinary_error_translation()
    {
        var loop = new FakeLoopModel(_ => throw Http400("Invalid API key provided"));

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                await ctx.Agent.ActAsync("do the thing");
            },
            Page(),
            loop.Model);

        Assert.Equal("MODEL_PROVIDER_FAILED", Assert.IsType<AgentException>(attempt.Error).Code);
        Assert.Single(loop.Calls);
    }

    /// <summary>A read-only project tool, so a turn can do something other than conclude.</summary>
    internal static IReadOnlyDictionary<string, AgentTool> Peek() => new Dictionary<string, AgentTool>
    {
        ["peek"] = new AgentTool(
            "looks around",
            new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() },
            false,
            (_, _) => Task.FromResult("looked")),
    };
}
