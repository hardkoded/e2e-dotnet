// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.ToolLoopExecutor;

/// <summary>
/// .NET-only: upstream tests its tool loop through the AI SDK and a scripted model (the "default ToolLoopAgent executor" tests of
/// <c>AgentActWithTheDefaultToolLoopAgentExecutorTests</c>). These read the public chassis, <see cref="ToolLoop.CreateToolLoopExecutor"/>,
/// with a vocabulary of its own, which the AI SDK shape of upstream's tools has no counterpart of here.
/// </summary>
public sealed partial class CreateToolLoopExecutorTests
{
    private const string Instruction = "increment the counter once";

    private static JsonElement Schema(string json) => JsonDocument.Parse(json).RootElement;

    private static ToolLoopTool ReadScreen(StepExecutorContext context) => new(
        new ModelTool { Name = "read_screen", Description = "Read the screen as text", Parameters = Schema("""{ "type": "object", "properties": {} }""") },
        async (_, token) => new ToolLoopResult((await context.ObserveAsync(cancellationToken: token)).Text));

    private static ToolLoopTool Tap(StepExecutorContext context) => new(
        new ModelTool { Name = "tap", Description = "Tap a node by its ref", Parameters = Schema("""{ "type": "object", "properties": { "ref": { "type": "string" } }, "required": ["ref"] }""") },
        async (call, token) =>
        {
            await context.Actions.TapAsync(new ExecutorTarget(call.Arguments.GetProperty("ref").GetString()!), token);
            return new ToolLoopResult("Tapped.");
        });

    private static IStepExecutor TapOnly(Func<StepExecutorContext, ToolLoopHelpers, IReadOnlyList<ToolLoopTool>>? tools = null, IAgentModel? model = null, Func<IReadOnlyList<ModelMessage>, PreparedTurn, PreparedMessages>? prepare = null) =>
        ToolLoop.CreateToolLoopExecutor(new ToolLoopExecutorOptions
        {
            Name = "tap-only",
            Version = "1",
            Model = model,
            System = _ => "Read the screen before acting.",
            BuildPrompt = context => Task.FromResult(context.Step.Instruction),
            Tools = tools ?? ((context, _) => [ReadScreen(context), Tap(context)]),
            PrepareMessages = prepare,
        });

    [Fact]
    public async Task Runs_a_vocabulary_of_its_own_and_adds_the_done_tool()
    {
        var model = new ScriptedModel(request => request.Messages.Count(message => message.Role == "tool") switch
        {
            0 => ModelResponses.Call("read_screen", new { }),
            1 => ModelResponses.Call("tap", new { @ref = IncrementRef(request.Messages.Last(message => message.Role == "tool").Content!) }),
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
            executor: TapOnly());

        Assert.Null(result.Error);
        Assert.Equal(["read_screen", "tap", "done"], model.Requests[0].Tools.Select(tool => tool.Name));
        Assert.Equal(Instruction, model.Requests[0].Messages[0].Content);
        Assert.Equal("Read the screen before acting.", model.Requests[0].System);
        Assert.Equal(3, act!.ModelCalls);
        Assert.Equal(1, act.Actions);
        Assert.All(model.Requests, request => Assert.True(request.MarkLatest));
    }

    [Fact]
    public async Task Appends_the_project_context_of_the_agent_to_its_guidance()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "nothing to do"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            Model = model,
            AgentExecutor = TapOnly(),
            AgentContext = "Plans are tiers.",
            BaseUrl = "https://counter.test",
            TestTitle = "context",
        });
        await session.App.OpenAsync("/");

        await session.Agent.ActAsync(Instruction);
        session.Complete();

        Assert.Equal("Read the screen before acting.\n\nProject context:\nPlans are tiers.", model.Requests[0].System);
    }

    [Fact]
    public async Task Hands_the_model_a_tool_that_threw_as_a_failed_call_and_goes_on()
    {
        var tools = new Func<StepExecutorContext, ToolLoopHelpers, IReadOnlyList<ToolLoopTool>>((_, _) =>
        [
            new ToolLoopTool(
                new ModelTool { Name = "lookup", Description = "Looks something up", Parameters = Schema("""{ "type": "object", "properties": {} }""") },
                (_, _) => throw new InvalidOperationException("the lookup is down")),
        ]);
        var model = new ScriptedModel(request => request.Messages.Any(message => message.Role == "tool")
            ? ModelResponses.Done("blocked", "the lookup is down", "ENVIRONMENT_UNAVAILABLE")
            : ModelResponses.Call("lookup", new { }));

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync(Instruction);
            },
            CounterPage.World(),
            model,
            executor: TapOnly(tools));

        var error = Assert.IsType<AgentException>(result.Error);
        Assert.Equal("ENVIRONMENT_UNAVAILABLE", error.Code);
        var answer = model.Requests[^1].Messages.First(message => message.Role == "tool").Content!;
        Assert.Contains("the lookup is down", answer, StringComparison.Ordinal);
        Assert.StartsWith("failed:", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ends_the_loop_when_a_tool_raises_a_hard_stop_of_the_step()
    {
        var tools = new Func<StepExecutorContext, ToolLoopHelpers, IReadOnlyList<ToolLoopTool>>((context, _) =>
        [
            new ToolLoopTool(
                new ModelTool { Name = "tap_twice", Description = "Taps the button twice", Parameters = Schema("""{ "type": "object", "properties": {} }""") },
                async (_, token) =>
                {
                    var observation = await context.ObserveAsync(cancellationToken: token);
                    var increment = TargetFor(observation.Text, "button \"Increment\"");
                    await context.Actions.TapAsync(increment, token);
                    await context.Actions.TapAsync(increment, token);
                    return new ToolLoopResult("Tapped twice.");
                }),
        ]);
        var model = new ScriptedModel(_ => ModelResponses.Call("tap_twice", new { }));

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync(Instruction, new ActOptions { MaxSteps = 1 });
            },
            CounterPage.World(),
            model,
            executor: TapOnly(tools));

        Assert.Equal("STEP_BUDGET_EXHAUSTED", Assert.IsType<AgentException>(result.Error).Code);
        Assert.Equal(1, model.CallCount);
    }

    [Fact]
    public async Task Offers_only_done_once_the_message_preparation_says_to_stop()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("failed", "no progress"));

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync(Instruction);
            },
            CounterPage.World(),
            model,
            executor: TapOnly(prepare: (messages, _) => new PreparedMessages(messages, "the step makes no progress")));

        Assert.Equal("ACTION_FAILED", Assert.IsType<AgentException>(result.Error).Code);
        Assert.Equal(["done"], model.Requests[0].Tools.Select(tool => tool.Name));
        Assert.Contains(model.Requests[0].Messages, message => message.Content?.Contains("the step makes no progress", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Tells_the_preparation_which_turn_it_is_and_what_the_turn_before_called()
    {
        var seen = new List<(int Step, string Calls)>();
        var model = new ScriptedModel(request => request.Messages.Count(message => message.Role == "tool") switch
        {
            0 => ModelResponses.Call("read_screen", new { }),
            _ => ModelResponses.Done("passed", "looked"),
        });

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync(Instruction);
            },
            CounterPage.World(),
            model,
            executor: TapOnly(prepare: (messages, turn) =>
            {
                seen.Add((turn.StepNumber, string.Join(",", turn.PreviousToolCalls)));
                return new PreparedMessages(messages);
            }));

        Assert.Null(result.Error);
        Assert.Equal([(0, ""), (1, "read_screen")], seen);
    }

    [Fact]
    public async Task Fails_with_MODEL_UNAVAILABLE_when_neither_the_loop_nor_the_agent_has_a_model()
    {
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync(Instruction);
            },
            CounterPage.World(),
            executor: TapOnly());

        Assert.Equal("MODEL_UNAVAILABLE", Assert.IsType<AgentException>(result.Error).Code);
    }

    [Fact]
    public async Task Brings_its_model_to_the_agent_for_the_act_and_the_judgments()
    {
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "nothing to do"));
        var executor = TapOnly(model: model);

        var agent = ResolvedAgent.From("default", new AgentOptions { Executor = executor });
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync(Instruction);
            },
            CounterPage.World(),
            executor: executor);

        Assert.Same(model, agent.Model);
        Assert.Same(model, agent.Judge);
        Assert.Null(result.Error);
        Assert.Equal(1, model.CallCount);
    }

    [Fact]
    public async Task Builds_the_built_in_agent_for_an_executor_to_wrap()
    {
        var inner = DefaultAgent.CreateBuiltInAgent(new BuiltInAgentOptions { System = "Be careful." });
        var wrapper = new TestExecutor("wrapper", (context, token) => inner.RunStepAsync(context, token));
        var model = new ScriptedModel(request => request.Messages.Any(message => message.Role == "tool")
            ? ModelResponses.Done("passed", "tapped")
            : ModelResponses.Tap("button", "Increment"));

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync(Instruction);
                await Expect.That(ctx.Screen.GetByRole("status")).ToHaveTextAsync("1");
            },
            CounterPage.World(),
            model,
            executor: wrapper);

        Assert.Null(result.Error);
        Assert.Contains("Be careful.", model.Requests[0].System, StringComparison.Ordinal);
        Assert.Contains("tap", model.Requests[0].Tools.Select(tool => tool.Name));
        Assert.Equal("e2e-default-agent", inner.Name);
    }

    [Fact]
    public async Task Refuses_to_run_the_built_in_agent_on_a_context_built_by_hand()
    {
        var inner = DefaultAgent.CreateBuiltInAgent();
        var context = new HandBuiltContext();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => inner.RunStepAsync(context, CancellationToken.None));

        Assert.Contains("a context built by hand", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_tool_loop_with_no_name()
    {
        var error = Assert.Throws<TestException>(() => ToolLoop.CreateToolLoopExecutor(new ToolLoopExecutorOptions
        {
            Name = " ",
            Tools = (_, _) => [],
            BuildPrompt = _ => Task.FromResult("go"),
        }));

        Assert.Equal("INVALID_ARGUMENT", error.Code);
    }

    private static string IncrementRef(string screen) => IncrementPattern().Match(screen).Groups["ref"].Value;

    [GeneratedRegex("button \"Increment\"[^\\n]*\\[ref=(?<ref>[^\\]]+)\\]")]
    private static partial Regex IncrementPattern();

    /// <summary>A context a test builds itself, which carries no screen to drive.</summary>
    private sealed class HandBuiltContext : StepExecutorContext
    {
        [SetsRequiredMembers]
        public HandBuiltContext()
        {
            Step = new ExecutorStep { Kind = ExecutorStepKind.Act, Instruction = "go" };
            Attempt = new ExecutorAttempt { TestId = "test", AttemptId = "attempt" };
            Target = new ExecutorTargetInfo { Name = "web", Platform = "web" };
            Budgets = new NoBudgets();
            Actions = null!;
            Model = new ScriptedModel(_ => ModelResponses.Done("passed", "ok"));
        }
    }

    private sealed class NoBudgets : IExecutorBudgets
    {
        public int MaxActions => 25;

        public int MaxModelCalls => 25;

        public int ActionsUsed() => 0;

        public int RemainingMs() => 60_000;

        public void RecordModelCall(ExecutorModelCall? usage = null)
        {
        }

        public Task<T> RunToolAsync<T>(string name, bool mutates, Func<Task<T>> body) => body();
    }
}
