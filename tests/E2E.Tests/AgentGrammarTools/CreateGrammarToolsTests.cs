// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Tests.AgentAct;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentGrammarTools;

/// <summary>
/// .NET-only: upstream's <c>grammar-tools-export</c> commit (tester-army/e2e#960) tests the export only in <c>tests/types/sdk-types.ts</c>
/// (see <c>SdkTypes/SdkTypesTests</c>). These run the public <see cref="AgentTools.CreateGrammarTools"/> and
/// <see cref="AgentTools.CreateVerdictTool"/> inside a tool loop of its own, and read the <see cref="GrammarToolsOptions.Guard"/> option.
/// </summary>
public sealed partial class CreateGrammarToolsTests
{
    private const string Instruction = "increment the counter once";

    private static ToolLoopTool ReadInbox() => new(
        new ModelTool { Name = "read_inbox", Description = "Read the inbox", Parameters = JsonDocument.Parse("""{ "type": "object", "properties": {} }""").RootElement },
        (_, _) => Task.FromResult(new ToolLoopResult("no mail")));

    private static IStepExecutor Executor(Func<StepExecutorContext, ToolLoopHelpers, IReadOnlyList<ToolLoopTool>> tools) =>
        ToolLoop.CreateToolLoopExecutor(new ToolLoopExecutorOptions
        {
            Name = "with-inbox",
            Version = "1",
            System = _ => DefaultAgent.BaseRules + "\n\nCheck the inbox with read_inbox when a step mentions an email.",
            BuildPrompt = context => Task.FromResult(context.Step.Instruction),
            Tools = tools,
        });

    [GeneratedRegex("button \"Increment\"[^\\n]*\\[ref=(?<ref>[^\\]]+)\\]")]
    private static partial Regex IncrementPattern();

    [Fact]
    public async Task Keeps_the_built_in_actions_beside_a_tool_of_its_own()
    {
        var model = new ScriptedModel(request => request.Messages.Count(message => message.Role == "tool") switch
        {
            0 => ModelResponses.Call("observe", new { }),
            1 => ModelResponses.Call("tap", new { @ref = IncrementPattern().Match(request.Messages.Last(message => message.Role == "tool").Content!).Groups["ref"].Value }),
            _ => ModelResponses.Done("passed", "tapped Increment"),
        });
        ActResult? act = null;

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                act = await ctx.Agent.ActAsync(Instruction);
                await Expect.That(ctx.Screen.GetByRole("status")).ToHaveTextAsync("1");
            },
            CounterPage.World(),
            model,
            executor: Executor((context, _) => [.. AgentTools.CreateGrammarTools(context), ReadInbox()]));

        Assert.Null(result.Error);
        var offered = model.Requests[0].Tools.Select(tool => tool.Name).ToList();
        Assert.Contains("observe", offered);
        Assert.Contains("tap", offered);
        Assert.Contains("read_inbox", offered);
        Assert.Equal("done", offered[^1]);
        Assert.StartsWith(DefaultAgent.BaseRules, model.Requests[0].System, StringComparison.Ordinal);
        Assert.EndsWith(AgentTools.VerdictRules, model.Requests[0].System, StringComparison.Ordinal);
        Assert.Equal(1, act!.Actions);
    }

    [Fact]
    public async Task Runs_every_tool_body_under_the_guard_and_reads_a_body_the_guard_answered_for_as_failed()
    {
        var guarded = new List<string>();
        var model = new ScriptedModel(request => request.Messages.Count(message => message.Role == "tool") switch
        {
            0 => ModelResponses.Call("observe", new { }),
            1 => ModelResponses.Call("tap", new { @ref = IncrementPattern().Match(request.Messages.Last(message => message.Role == "tool").Content!).Groups["ref"].Value }),
            _ => ModelResponses.Done("failed", "the guard stopped the tap"),
        });
        var options = new GrammarToolsOptions
        {
            Guard = async body =>
            {
                var text = await body();
                guarded.Add(text);
                return text.StartsWith("Tapped", StringComparison.Ordinal) ? "stopped by the guard" : text;
            },
        };

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync(Instruction);
            },
            CounterPage.World(),
            model,
            executor: Executor((context, _) => AgentTools.CreateGrammarTools(context, options)));

        Assert.Equal(2, guarded.Count);
        Assert.Contains(model.Requests[^1].Messages, message => message.Role == "tool" && message.Content == "stopped by the guard");
        Assert.IsType<AgentException>(result.Error);
    }

    [Fact]
    public async Task Fails_a_call_whose_body_the_guard_never_ran()
    {
        var model = new ScriptedModel(request => request.Messages.Any(message => message.Role == "tool")
            ? ModelResponses.Done("blocked", "the step is concluding", "ENVIRONMENT_UNAVAILABLE")
            : ModelResponses.Call("observe", new { }));
        var options = new GrammarToolsOptions { Guard = _ => Task.FromResult("The step is concluding; nothing more runs.") };

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync(Instruction);
            },
            CounterPage.World(),
            model,
            executor: Executor((context, _) => AgentTools.CreateGrammarTools(context, options)));

        Assert.IsType<AgentException>(result.Error);
        Assert.Equal("The step is concluding; nothing more runs.", model.Requests[^1].Messages.First(message => message.Role == "tool").Content);
    }

    [Fact]
    public void Collects_the_first_accepted_verdict_and_refuses_a_blocked_one_without_a_blockable_code()
    {
        var verdict = AgentTools.CreateVerdictTool();
        Assert.False(verdict.Concluded);
        Assert.Null(verdict.Verdict);

        var refused = verdict.Execute(Done("blocked", "no way in", null));
        Assert.True(refused.Failed);
        Assert.False(verdict.Concluded);

        var passed = verdict.Execute(Done("passed", "all good", "ACTION_FAILED"));
        Assert.False(passed.Failed);
        Assert.Contains("ignored", passed.Content, StringComparison.Ordinal);
        Assert.Equal(new StepVerdict(StepVerdictStatus.Passed, "all good"), verdict.Verdict);

        var late = verdict.Execute(Done("failed", "changed my mind", "ACTION_FAILED"));
        Assert.Equal("The step already concluded.", late.Content);
        Assert.Equal(StepVerdictStatus.Passed, verdict.Verdict!.Value.Status);
    }

    private static ModelToolCall Done(string status, string summary, string? code) => new()
    {
        Id = "call-1",
        Name = VerdictTool.Name,
        Arguments = JsonSerializer.SerializeToElement(code is null ? new { status, summary } : (object)new { status, summary, code }),
    };
}
