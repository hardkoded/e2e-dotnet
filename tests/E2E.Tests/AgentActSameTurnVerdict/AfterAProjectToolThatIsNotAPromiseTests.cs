// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using static E2E.Tests.AgentAct.CounterPage;
using static E2E.Tests.AgentActSameTurnVerdict.AgentActSameTurnVerdictHelpers;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentActSameTurnVerdict;

/// <summary>
/// Upstream's <c>describe('after a project tool that is not a promise')</c>. The port's <see cref="ToolLoopTool"/> always returns a
/// task, so the plain value and the async iterable of upstream's tool have no counterpart here: the cases that return a failure at once
/// and that throw are ported; the iterables are not.
/// </summary>
public sealed class AfterAProjectToolThatIsNotAPromiseTests
{
    /// <summary>A tool-loop executor whose only tool is <c>check</c>, as the project wrote it (upstream's <c>checkExecutor</c>).</summary>
    private static IStepExecutor CheckExecutor(Func<ToolLoopResult> check) =>
        ToolLoop.CreateToolLoopExecutor(
            new ToolLoopExecutorOptions
            {
                Name = "check-only",
                Version = "1",
                BuildPrompt = context => Task.FromResult(context.Step.Instruction),
                Tools = (_, _) =>
                    [
                        new ToolLoopTool(
                            new ModelTool
                            {
                                Name = "check",
                                Description = "Run the check",
                                Parameters = JsonDocument
                                    .Parse("""{ "type": "object", "properties": {} }""")
                                    .RootElement,
                            },
                            (_, _) => Task.FromResult(check())
                        ),
                    ],
            }
        );

    [Theory]
    [InlineData("a failure it returns at once")]
    [InlineData("a tool that throws")]
    public async Task Refuses_the_verdict_after(string kind)
    {
        var model = new ScriptedModel(request =>
            request.Messages.Any(message => message.Role == "tool")
                ? ModelResponses.Done("failed", "the check failed", "ACTION_FAILED")
                : Batch(ModelResponses.Call("check", new { }), Passed)
        );
        var executor = CheckExecutor(() =>
            kind == "a tool that throws"
                ? throw new InvalidOperationException("no rows")
                : new ToolLoopResult("Tool \"check\" failed: no rows")
        );
        var attempt = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("run the check");
            },
            FixtureWorld(),
            model,
            executor: executor,
            debug: true
        );

        Assert.NotNull(attempt.Error);
        Assert.Contains("the check failed", attempt.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Rejected: check failed in this turn", ToolResult(model, 1, 1), StringComparison.Ordinal);
    }
}
