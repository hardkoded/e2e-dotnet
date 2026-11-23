// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.AgentAct.CounterPage;
using static E2E.Tests.AgentActSameTurnVerdict.AgentActSameTurnVerdictHelpers;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentActSameTurnVerdict;

/// <summary>
/// Upstream's <c>describe('a verdict sent in the same turn as the last action')</c> (<c>agent-act-same-turn-verdict.test.ts</c>): a
/// <c>complete_step</c> the model sends in the same turn as the step's last action waits for that action instead of skipping it, ends the
/// step without a turn spent reading the result, and is refused when the action failed or had no visible effect. A call after it in the
/// turn never runs.
/// </summary>
public sealed class AVerdictSentInTheSameTurnAsTheLastActionTests
{
    [Fact]
    public async Task Runs_the_action_first_and_ends_the_step_in_one_model_call()
    {
        var model = new ScriptedModel(request =>
            request.Messages.Count(message => message.Role == "tool") > 0
                ? throw new InvalidOperationException("unexpected turn 2")
                : Batch(ModelResponses.Tap("button", "Increment"), Passed)
        );
        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("tap Increment once");
                await Expect.That(ctx.Screen.GetByRole("status")).ToHaveTextAsync("1");
            },
            FixtureWorld(),
            model,
            debug: true
        );

        Assert.Null(attempt.Error);
        var step = ActStep(attempt.Steps);
        Assert.Equal(StepStatus.Passed, step.Status);
        Assert.Equal(1, step.Metrics!.ActionSteps);
        Assert.Equal(1, step.Metrics.ModelCalls);
        Assert.Single(model.Requests);

        // The rule that tells the model it may do this goes out with every step.
        Assert.Contains(
            "send that action and then complete_step with status \"passed\" in one reply",
            model.Requests[0].System,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Is_refused_when_the_action_had_no_visible_effect_and_the_model_concludes_again()
    {
        var model = new ScriptedModel(request =>
            request.Messages.Any(message => message.Role == "tool")
                ? ModelResponses.Done("failed", "the dead end button did nothing", "ACTION_FAILED")
                : Batch(ModelResponses.Tap("button", "Dead end"), Passed)
        );
        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/delayed");
                await ctx.Agent.ActAsync("tap the dead end button");
            },
            FixtureWorld(),
            model,
            debug: true
        );

        Assert.NotNull(attempt.Error);
        Assert.Contains("the dead end button did nothing", attempt.Error.Message, StringComparison.Ordinal);
        Assert.Contains(
            "No listed node changed within the wait after this action",
            ToolResult(model, 1, 0),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "Rejected: tap had no visible effect in this turn",
            ToolResult(model, 1, 1),
            StringComparison.Ordinal
        );
        Assert.Equal(2, ActStep(attempt.Steps).Metrics!.ModelCalls);
    }

    [Fact]
    public async Task Is_taken_when_only_an_earlier_action_of_the_turn_changed_nothing()
    {
        var model = new ScriptedModel(request =>
            request.Messages.Any(message => message.Role == "tool")
                ? throw new InvalidOperationException("unexpected turn 2")
                : Batch(ModelResponses.Tap("button", "Dead end"), ModelResponses.Tap("button", "Continue"), Passed)
        );
        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/delayed");
                await ctx.Agent.ActAsync("tap the dead end button, then tap Continue");
                await Expect.That(ctx.Screen.GetByRole("heading", "Second view")).ToBeVisibleAsync();
            },
            FixtureWorld(),
            model,
            debug: true
        );

        Assert.Null(attempt.Error);
        var step = ActStep(attempt.Steps);
        Assert.Equal(2, step.Metrics!.ActionSteps);
        Assert.Equal(1, step.Metrics.ModelCalls);
    }

    [Fact]
    public async Task Is_refused_when_the_action_failed()
    {
        var model = new ScriptedModel(request =>
            request.Messages.Any(message => message.Role == "tool")
                ? Batch(ModelResponses.Tap("button", "Increment"), Passed)
                : Batch(ModelResponses.Call("tap", new { target = "n99999" }), Passed)
        );
        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("tap Increment once");
                await Expect.That(ctx.Screen.GetByRole("status")).ToHaveTextAsync("1");
            },
            FixtureWorld(),
            model,
            debug: true
        );

        Assert.Null(attempt.Error);
        Assert.Matches("^tap #?n99999 failed: ", ToolResult(model, 1, 0));
        Assert.Contains("Rejected: tap failed in this turn", ToolResult(model, 1, 1), StringComparison.Ordinal);
        Assert.Equal(2, ActStep(attempt.Steps).Metrics!.ModelCalls);
    }

    [Fact]
    public async Task Is_refused_when_the_SDK_turned_the_action_away_before_it_ran()
    {
        var model = new ScriptedModel(request =>
            request.Messages.Any(message => message.Role == "tool")
                ? Batch(ModelResponses.Tap("button", "Increment"), Passed)
                : Batch(ModelResponses.Call("tap", new { target = 42 }), Passed)
        );
        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("tap Increment once");
                await Expect.That(ctx.Screen.GetByRole("status")).ToHaveTextAsync("1");
            },
            FixtureWorld(),
            model,
            debug: true
        );

        Assert.Null(attempt.Error);
        Assert.Contains("Rejected: tap did not run in this turn", ToolResult(model, 1, 1), StringComparison.Ordinal);
        var step = ActStep(attempt.Steps);
        Assert.Equal(1, step.Metrics!.ActionSteps);
        Assert.Equal(2, step.Metrics.ModelCalls);
    }

    [Fact]
    public async Task Runs_nothing_the_model_sent_after_it()
    {
        var model = new ScriptedModel(request =>
            request.Messages.Any(message => message.Role == "tool")
                ? throw new InvalidOperationException("unexpected turn 2")
                : Batch(Passed, ModelResponses.Tap("button", "Increment"))
        );
        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("tap Increment once");
                await Expect.That(ctx.Screen.GetByRole("status")).ToHaveTextAsync("0");
            },
            FixtureWorld(),
            model,
            debug: true
        );

        Assert.Null(attempt.Error);
        var step = ActStep(attempt.Steps);
        Assert.Equal(0, step.Metrics!.ActionSteps);
        Assert.Equal(1, step.Metrics.ModelCalls);
    }
}
