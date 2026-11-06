// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.ToolLoopToolChoice;

/// <summary>The "tool loop downgraded tool choice" describe. The port ends a step with <c>done</c>, where upstream's tool is <c>complete_step</c>.</summary>
public sealed class ToolLoopDowngradedToolChoiceTests
{
    /// <summary>The warning the AI SDK's Anthropic provider attaches when it sends <c>auto</c> in place of a forced choice.</summary>
    private static readonly ModelWarning Downgrade = new(
        "unsupported",
        "toolChoice",
        "toolChoice 'required' is not supported by this model because it rejects forced tool use. Using 'auto' instead.");

    private static DocumentWorld Page() => new DocumentWorld().Map("/start", page => page.Button("Peek"));

    private static ModelResponse Done() => ModelResponses.Done("passed", "done");

    /// <summary>A provider that rewrites every forced choice to <c>auto</c> and says so only as a call warning.</summary>
    private static Func<LoopCall, ModelResponse> DowngradingForcedChoice(Func<LoopCall, ModelResponse> respond) => call =>
    {
        var answer = respond(call);
        return call.ToolChoice == "auto"
            ? answer
            : new ModelResponse { Content = answer.Content, ToolCalls = answer.ToolCalls, Warnings = [Downgrade] };
    };

    [Fact]
    public async Task Continues_the_step_with_auto_under_the_same_instructions_once_the_provider_reports_a_downgrade()
    {
        var loop = new FakeLoopModel(DowngradingForcedChoice(call => call.Turn == 1 ? ModelResponses.Call("peek", new { }) : Done()));
        ActResult? result = null;

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                result = await ctx.Agent.ActAsync("look around");
            },
            Page(),
            loop.Model,
            agentTools: ToolLoopForcedToolChoiceTests.Peek());

        Assert.Null(attempt.Error);
        // The downgraded turn succeeded, so nothing is sent again, and the next request keeps the system prompt its reasoning is bound to.
        Assert.Equal(["required", "auto"], loop.Calls.Select(call => call.ToolChoice));
        Assert.Contains("Reply with tool calls only", loop.Calls[0].System, StringComparison.Ordinal);
        Assert.Equal(loop.Calls[0].System, loop.Calls[1].System);
        Assert.Equal(2, result!.ModelCalls);
    }

    [Fact]
    public async Task Retries_with_auto_when_a_downgraded_turn_answers_in_prose()
    {
        var loop = new FakeLoopModel(DowngradingForcedChoice(call => call.Turn == 1 ? new ModelResponse { Content = "Let me look around first." } : Done()));
        ActResult? result = null;

        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/start");
                result = await ctx.Agent.ActAsync("look around");
            },
            Page(),
            loop.Model);

        Assert.Null(attempt.Error);
        Assert.Equal(["required", "auto"], loop.Calls.Select(call => call.ToolChoice));
        Assert.Equal(loop.Calls[0].Prompt, loop.Calls[1].Prompt);
        Assert.Equal(loop.Calls[0].System, loop.Calls[1].System);
        // The prose reply was answered and billed before the loop sent the request again.
        Assert.Equal(2, result!.ModelCalls);
    }

    [Fact]
    public async Task Starts_later_steps_on_the_same_model_in_auto()
    {
        var loop = new FakeLoopModel(DowngradingForcedChoice(_ => Done()));

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
        Assert.Equal(["required", "auto"], loop.Calls.Select(call => call.ToolChoice));
        Assert.Equal(loop.Calls[0].System, loop.Calls[1].System);
    }

    [Fact]
    public async Task Ignores_warnings_about_other_features()
    {
        var loop = new FakeLoopModel(_ => new ModelResponse { ToolCalls = Done().ToolCalls, Warnings = [new ModelWarning("unsupported", "temperature")] });

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
        Assert.Equal(["required", "required"], loop.Calls.Select(call => call.ToolChoice));
    }
}
