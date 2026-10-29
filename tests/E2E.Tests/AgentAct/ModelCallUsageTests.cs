// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentAct;

/// <summary>.NET-only: the step records the cache split and the cost that the provider reported, on the tool loop and on the judgment call.</summary>
public sealed class ModelCallUsageTests
{
    [Fact]
    public async Task Records_the_cache_split_and_the_cost_of_the_tool_loop_and_of_the_judgment()
    {
        var model = new ScriptedModel(request => new ModelResponse
        {
            ToolCalls = request.Messages.Any(message => message.Role == "tool") || request.Messages.Any(message => message.Content?.Contains("Statement:", StringComparison.Ordinal) == true)
                ? ModelResponses.Done("passed", "ok").ToolCalls
                : ModelResponses.Tap("button", "Increment").ToolCalls,
            Usage = new ModelCallUsage { InputTokens = 100, OutputTokens = 3, CacheReadTokens = 60, CacheWriteTokens = 10, EstimatedCostUsd = 0.25 },
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            Model = model,
            BaseUrl = "https://billing.test",
            TestTitle = "usage > case",
            AssertionTimeout = TimeSpan.FromSeconds(2),
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            TestTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(5),
        });

        await session.Context.App.OpenAsync("/");
        await session.Context.Agent.ActAsync("increment the counter once");
        await session.Context.Agent.AssertAsync("the counter shows 1");
        session.Complete(null);

        foreach (var api in new[] { "agent.act", "agent.assert" })
        {
            var info = session.Steps.Single(step => step.Api == api).Model!;
            Assert.Equal(60 * info.Calls, info.CacheReadTokens);
            Assert.Equal(10 * info.Calls, info.CacheWriteTokens);
            Assert.Equal(0.25 * info.Calls, info.EstimatedCostUsd);
        }
    }
}
