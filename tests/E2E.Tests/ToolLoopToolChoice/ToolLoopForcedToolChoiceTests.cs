// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.ToolLoopToolChoice;

/// <summary>
/// The port ends a step with a <c>done</c> tool, not <c>complete_step</c>. The tests below keep upstream's names, assert <c>done</c>
/// where upstream asserts <c>complete_step</c>, and are <c>Partial_</c> where upstream's check needs a model that refuses a forced
/// choice: the refused request and the retry under <c>auto</c>. The port sends the tool choice (<see cref="ModelRequest.ToolChoice"/>)
/// but does not yet recognize a refusal. Not ported here (tracked in #1157 for the next PR): "retries with auto, under the same
/// instructions, when the model rejects a forced choice", "remembers the refusal for later steps on the same model", "leaves a 400
/// about anything else to the ordinary error translation", the four "downgraded tool choice" cases, "isForcedToolChoiceRejected" and
/// "isForcedToolCallSkipped".
/// </summary>
public sealed class ToolLoopForcedToolChoiceTests
{
    private static DocumentWorld Page() => new DocumentWorld().Map("/start", page => page.Button("Peek"));

    /// <summary>Partial: the port's model is never refused a forced choice, so there is no refused request, and the notice names done.</summary>
    [Fact]
    public async Task Partial_Asks_again_when_a_turn_comes_back_as_prose_without_a_tool_call()
    {
        var seen = new List<(int Count, string Role, string? Content)>();
        var model = new ScriptedModel(request =>
        {
            seen.Add((request.Messages.Count, request.Messages[^1].Role, request.Messages[^1].Content));
            return seen.Count == 1 ? new ModelResponse { Content = "I will look around first." } : ModelResponses.Done("passed", "Done.");
        });
        ActResult? result = null;

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                result = await ctx.Agent.ActAsync("do the thing");
            },
            Page(),
            model);

        Assert.Null(attempt.Error);
        Assert.Equal(2, model.CallCount);
        Assert.Equal("user", seen[1].Role);
        Assert.Contains("Your last reply had no tool call", seen[1].Content, StringComparison.Ordinal);
        Assert.Equal(seen[0].Count + 2, seen[1].Count);
        // The prose turn is one the step paid for.
        Assert.Equal(2, result!.ModelCalls);
    }

    /// <summary>Partial: there is no refused request before the three prose turns, so the model is called three times, not four.</summary>
    [Fact]
    public async Task Partial_Fails_with_STEP_NO_CONCLUSION_when_prose_replies_use_up_the_turns()
    {
        var model = new ScriptedModel(_ => new ModelResponse { Content = "Thinking about it." });

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                await ctx.Agent.ActAsync("do the thing", new ActOptions { MaxModelCalls = 3 });
            },
            Page(),
            model);

        Assert.Equal("STEP_NO_CONCLUSION", Assert.IsType<AgentException>(attempt.Error).Code);
        // The budget, not the loop, ends it.
        Assert.Equal(3, model.CallCount);
    }

    /// <summary>Partial: without a refused forced choice the loop is on <c>auto</c> from the start (until the retry lands), so the port asserts the offered tool list and the <c>auto</c> choice without a refused request.</summary>
    [Fact]
    public async Task Partial_Offers_only_complete_step_under_auto_on_the_forced_conclusion_turns()
    {
        var model = new ScriptedModel(request =>
            request.Tools.Count == 1 && request.Tools[0].Name == "done" ? ModelResponses.Done("passed", "Done.") : ModelResponses.Call("peek", new { }));

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                await ctx.Agent.ActAsync("look until told to stop", new ActOptions { MaxModelCalls = 4 });
            },
            Page(),
            model,
            agentTools: Peek());

        Assert.Null(attempt.Error);
        Assert.Equal(["done"], model.Requests[^1].Tools.Select(tool => tool.Name));
        Assert.Equal(ModelToolChoice.Auto, model.Requests[^1].ToolChoice);
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
