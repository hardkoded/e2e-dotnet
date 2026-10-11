// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.AgentAct;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentActStress;

public sealed class InputBoundariesAndAdversarialLoopsTests
{
    [Fact]
    public async Task Rejects_params_past_the_canonical_size_and_depth_bounds()
    {
        var model = new ScriptedModel(_ => throw new InvalidOperationException("no model call expected"));
        var oversized = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("probe", new ActOptions { Params = new Dictionary<string, object?> { ["blob"] = new string('x', 70_000) } });
            },
            CounterPage.World(),
            model);
        object value = new Dictionary<string, object?> { ["leaf"] = true };
        for (var level = 0; level < 40; level++)
        {
            value = new Dictionary<string, object?> { ["nested"] = value };
        }

        var tooDeep = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("probe", new ActOptions { Params = new Dictionary<string, object?> { ["value"] = value } });
            },
            CounterPage.World(),
            model);

        Assert.Contains("canonical bytes", Assert.IsType<TestException>(oversized.Error).Message, StringComparison.Ordinal);
        Assert.Contains("nesting", Assert.IsType<TestException>(tooDeep.Error).Message, StringComparison.Ordinal);
    }

    // The port has no forced-conclusion turn, so the model that keeps tapping meets its loop guard and its call budget.
    [Fact]
    public async Task Survives_a_model_that_defies_the_forced_conclusion()
    {
        // Always tap, even when only done is offered.
        var model = new ScriptedModel(_ => ModelResponses.Tap("button", "Increment"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("never conclude", new ActOptions { MaxModelCalls = 6 });
            },
            CounterPage.World(),
            model);

        // Any sane terminal code is fine; hanging or passing is not.
        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Contains(error.Code, new[] { "STEP_NO_CONCLUSION", "MODEL_PROVIDER_FAILED", "MODEL_OUTPUT_INVALID", "STEP_BUDGET_EXHAUSTED" });
    }

    [Fact]
    public async Task Runs_sequential_act_steps_with_fresh_budgets_each()
    {
        var results = new List<ActResult>();
        var model = new ScriptedModel(request => request.Messages.Any(message => message.Role == "tool")
            ? ModelResponses.Done("passed", "tapped once")
            : ModelResponses.Tap("button", "Increment"));
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                for (var step = 0; step < 3; step++)
                {
                    results.Add(await ctx.Agent.ActAsync("tap once"));
                }

                await Expect.That(ctx.Screen.GetByRole("status")).ToHaveTextAsync("3");
            },
            CounterPage.World(),
            model);

        Assert.Null(result.Error);
        Assert.Equal(3, results.Count);
        Assert.All(results, step => Assert.Equal(1, step.Actions));
    }
}
