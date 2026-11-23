// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using static E2E.Tests.AgentAct.CounterPage;
using static E2E.Tests.AgentActSameTurnVerdict.AgentActSameTurnVerdictHelpers;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentActSameTurnVerdict;

/// <summary>
/// Upstream's <c>describe('after a project tool that is not a promise')</c>. The port's tool answers a task or an async sequence, the
/// counterparts of upstream's plain value and async iterable.
/// </summary>
public sealed class AfterAProjectToolThatIsNotAPromiseTests
{
    private const string Failure = "Tool \"check\" failed: no rows";

    /// <summary>An async sequence of <paramref name="values"/>, then <paramref name="error"/> thrown when one is given (upstream's <c>outputs</c>).</summary>
    private static async IAsyncEnumerable<ToolLoopResult> Outputs(string[] values, Exception? error = null)
    {
        foreach (var value in values)
        {
            await Task.Yield();
            yield return new ToolLoopResult(value);
        }

        if (error is not null)
        {
            throw error;
        }
    }

    /// <summary>A tool-loop executor whose only tool is <c>check</c>, as the project wrote it (upstream's <c>checkExecutor</c>).</summary>
    private static IStepExecutor CheckExecutor(Func<ModelTool, ToolLoopTool> check) =>
        ToolLoop.CreateToolLoopExecutor(
            new ToolLoopExecutorOptions
            {
                Name = "check-only",
                Version = "1",
                BuildPrompt = context => Task.FromResult(context.Step.Instruction),
                Tools = (_, _) =>
                    [
                        check(
                            new ModelTool
                            {
                                Name = "check",
                                Description = "Run the check",
                                Parameters = JsonDocument
                                    .Parse("""{ "type": "object", "properties": {} }""")
                                    .RootElement,
                            }
                        ),
                    ],
            }
        );

    [Theory]
    [InlineData("a failure it returns at once")]
    [InlineData("an iterable that throws")]
    [InlineData("an iterable whose last output is a failure")]
    public async Task Refuses_the_verdict_after(string kind)
    {
        var model = new ScriptedModel(request =>
            request.Messages.Any(message => message.Role == "tool")
                ? ModelResponses.Done("failed", "the check failed", "ACTION_FAILED")
                : Batch(ModelResponses.Call("check", new { }), Passed)
        );
        var executor = CheckExecutor(definition =>
            kind switch
            {
                "a failure it returns at once" => new ToolLoopTool(definition, (_, _) =>
                    Task.FromResult(new ToolLoopResult(Failure))
                ),
                "an iterable that throws" => ToolLoopTool.FromOutputs(definition, (_, _) =>
                    Outputs(["checking"], new InvalidOperationException("no rows"))
                ),
                _ => ToolLoopTool.FromOutputs(definition, (_, _) => Outputs(["checking", Failure])),
            }
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

    [Fact]
    public async Task Takes_the_verdict_after_an_iterable_that_ends_well_and_the_model_reads_its_last_output()
    {
        var model = new ScriptedModel(request =>
            request.Messages.Any(message => message.Role == "tool")
                ? throw new InvalidOperationException("unexpected turn 2")
                : Batch(ModelResponses.Call("check", new { }), Passed)
        );
        var executor = CheckExecutor(definition =>
            ToolLoopTool.FromOutputs(definition, (_, _) => Outputs(["checking", "all rows present"]))
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

        Assert.Null(attempt.Error);
        Assert.Single(model.Requests);
    }
}
