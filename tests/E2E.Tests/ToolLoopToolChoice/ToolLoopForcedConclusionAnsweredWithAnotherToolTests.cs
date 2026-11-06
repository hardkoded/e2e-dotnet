// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.ToolLoopToolChoice;

/// <summary>
/// The "tool loop forced conclusion answered with another tool" describe. The port offers only <c>done</c> on the forced turns, so the
/// tool names in the assertions are <c>done</c>, not upstream's <c>complete_step</c>.
/// </summary>
public sealed class ToolLoopForcedConclusionAnsweredWithAnotherToolTests
{
    private static DocumentWorld Page() => new DocumentWorld().Map("/start", page => page.Button("Peek"));

    /// <summary>
    /// Partial: the port has no provider metadata on a tool call, so it leaves out upstream's <c>toolCallOptions</c> check.
    /// </summary>
    [Fact]
    public async Task Partial_Sends_the_call_back_as_an_error_and_asks_again_on_the_same_turn_budget()
    {
        var turn = 0;
        List<string?> results = [];
        var model = new ScriptedModel(request =>
        {
            if (++turn != 3)
            {
                return ModelResponses.Call("peek", new { });
            }

            // The history is kept by reference, so it is read as the third request sees it.
            results = [.. request.Messages.Where(message => message.Role == "tool").Select(message => message.Content)];
            return ModelResponses.Done("passed", "Done.");
        });
        ActResult? result = null;

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                result = await ctx.Agent.ActAsync("look until told to stop", new ActOptions { MaxModelCalls = 3 });
            },
            Page(),
            model,
            agentTools: ToolLoopForcedToolChoiceTests.Peek());

        Assert.Null(attempt.Error);
        // Interim: the loop starts with auto until the forced-choice retry lands, so no turn forces a tool yet.
        Assert.Equal(["auto", "auto", "auto"], model.Requests.Select(request => request.ToolChoice!.Type == "tool" ? "tool:" + request.ToolChoice.ToolName : request.ToolChoice.Type));
        // The first turn offers every tool; the last two are the forced-conclusion turns, which offer only done.
        Assert.Contains(model.Requests[0].Tools, tool => tool.Name == "peek");
        Assert.Equal(["done"], model.Requests[1].Tools.Select(tool => tool.Name));
        Assert.Equal(["done"], model.Requests[2].Tools.Select(tool => tool.Name));
        Assert.Equal(2, results.Count);
        Assert.Equal("looked", results[0]);
        Assert.Contains("peek did not run", results[1], StringComparison.Ordinal);
        // The wrong-tool reply was answered and billed.
        Assert.Equal(3, result!.ModelCalls);
    }

    [Fact]
    public async Task Fails_with_STEP_NO_CONCLUSION_when_the_model_never_calls_complete_step()
    {
        var model = new ScriptedModel(_ => ModelResponses.Call("peek", new { }));

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                await ctx.Agent.ActAsync("look forever", new ActOptions { MaxModelCalls = 3 });
            },
            Page(),
            model,
            agentTools: ToolLoopForcedToolChoiceTests.Peek());

        Assert.Equal("STEP_NO_CONCLUSION", Assert.IsType<AgentException>(attempt.Error).Code);
        Assert.Equal(3, model.CallCount);
    }
}
