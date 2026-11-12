// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.ToolLoopOverflow;

/// <summary>The <c>it</c> outside any <c>describe</c> of upstream's <c>tool-loop-overflow.test.ts</c> that tester-army/e2e#1568 added.</summary>
public sealed class ToolLoopOverflowTests
{
    [Fact]
    public async Task Fails_an_overflow_as_inconclusive_without_shrinking_the_history_when_InconclusiveOnOverflow_is_set()
    {
        var model = new ScriptedModel(_ => throw new AgentException("CONTEXT_OVERFLOW", "The model request exceeded the context window: prompt is too long: 300000 tokens > 200000 maximum", blocked: true));
        var executor = ToolLoop.CreateToolLoopExecutor(new ToolLoopExecutorOptions
        {
            Name = "evidence-keeping-loop",
            InconclusiveOnOverflow = true,
            Tools = (_, _) => [],
            BuildPrompt = async context => (await context.ObserveAsync()).Text,
        });
        var world = new DocumentWorld().Map("/big", page =>
        {
            for (var i = 0; i < 600; i++)
            {
                page.Paragraph("row " + i + " " + new string('x', 40));
            }
        });
        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/big");
                await ctx.Agent.AssertAsync("inspect the evidence");
            },
            world,
            model,
            executor: executor);

        Assert.Equal("ASSERTION_INCONCLUSIVE", Assert.IsType<AgentException>(attempt.Error).Code);
        Assert.Equal(1, model.CallCount);
    }
}
