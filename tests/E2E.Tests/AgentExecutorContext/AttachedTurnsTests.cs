// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentExecutorContext;

/// <summary>
/// An executor keeps the model turns of its step on the step record through <c>AttachTurns</c>, and the tool loop does it for every
/// turn it takes. No upstream <c>it</c> covers this (upstream's tests stub <c>attachTurns</c>), so these tests are .NET-only.
/// </summary>
public sealed class AttachedTurnsTests
{
    [Fact]
    public async Task Keeps_the_turns_a_custom_executor_attached_with_a_secret_it_knows_redacted()
    {
        var executor = new TestExecutor("talker", (context, _) =>
        {
            context.AttachTurns([new StepTurn(1, ["type({\"text\":\"admin-pass\"})"], "typed admin-pass")]);
            return Task.FromResult(Passed("done"));
        });
        await using var session = await StartAsync(executor);

        await session.Agent.ActAsync(
            "talk",
            new ActOptions { Params = new Dictionary<string, object?> { ["password"] = Secret.Create("admin.password", "admin-pass", "password") } });

        var turn = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<StepTurn>>(AgentStep(session).Turns));
        Assert.Equal(1, turn.Index);
        Assert.Equal(["type({\"text\":\"<secret:admin.password>\"})"], turn.Calls);
        Assert.Equal("typed <secret:admin.password>", turn.Outcome);
    }

    [Fact]
    public async Task Keeps_nothing_when_the_executor_attached_no_turns()
    {
        await using var session = await StartAsync(new TestExecutor("quiet", (_, _) => Task.FromResult(Passed("done"))));

        await session.Agent.ActAsync("do nothing");

        Assert.Null(AgentStep(session).Turns);
    }

    [Fact]
    public async Task The_tool_loop_records_each_turn_with_its_calls_and_results_and_notes_a_reply_without_a_tool_call()
    {
        var calls = 0;
        var model = new ScriptedModel(_ => ++calls switch
        {
            1 => new ModelResponse { Content = "I will look around first." },
            2 => ModelResponses.Tap("button", "Increment"),
            _ => ModelResponses.Done("passed", "Counted."),
        });
        await using var session = await StartAsync(executor: null, model);

        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("increment the counter");

        var turns = Assert.IsAssignableFrom<IReadOnlyList<StepTurn>>(AgentStep(session).Turns);
        Assert.Equal([1, 2, 3], turns.Select(turn => turn.Index));
        Assert.Empty(turns[0].Calls);
        Assert.Equal("assistant: I will look around first.\n[loop] turn 1 made no tool call: asking for one", turns[0].Outcome);
        Assert.StartsWith("tap(", Assert.Single(turns[1].Calls), StringComparison.Ordinal);
        Assert.StartsWith("[tap] ", turns[1].Outcome, StringComparison.Ordinal);
        Assert.StartsWith("done(", Assert.Single(turns[2].Calls), StringComparison.Ordinal);
        Assert.Equal("[done] done passed", turns[2].Outcome);
    }

    [Fact]
    public async Task The_tool_loop_keeps_the_last_twelve_turns_with_calls_and_arguments_clipped()
    {
        var calls = 0;
        var model = new ScriptedModel(_ => ++calls <= 14
            ? ModelResponses.Call("navigate", new { url = "/" + new string('x', 500) + calls })
            : ModelResponses.Done("passed", "Done."));
        await using var session = await StartAsync(executor: null, model);

        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("wander");

        var turns = Assert.IsAssignableFrom<IReadOnlyList<StepTurn>>(AgentStep(session).Turns);
        Assert.Equal(12, turns.Count);
        Assert.Equal(4, turns[0].Index);
        Assert.Equal(15, turns[^1].Index);
        Assert.All(turns.SelectMany(turn => turn.Calls), call => Assert.True(call.Length <= 200 + "…[truncated]".Length));
    }

    [Fact]
    public async Task Bounds_the_turns_a_custom_executor_attached_and_redacts_before_it_cuts()
    {
        var executor = new TestExecutor("wordy", (context, _) =>
        {
            // The secret starts 5 characters before the 2048-character cut of an outcome and 195 before the 200-character cut of a call.
            context.AttachTurns(
            [
                .. Enumerable.Range(1, 20).Select(index => new StepTurn(
                    index,
                    [.. Enumerable.Range(0, 10).Select(_ => new string('c', 195) + "hunter2xyz")],
                    new string('o', 2043) + "hunter2xyz")),
            ]);
            return Task.FromResult(Passed("done"));
        });
        await using var session = await StartAsync(executor);

        await session.Agent.ActAsync(
            "talk",
            new ActOptions { Params = new Dictionary<string, object?> { ["pw"] = Secret.Create("pw", "hunter2xyz", "password") } });

        var turns = Assert.IsAssignableFrom<IReadOnlyList<StepTurn>>(AgentStep(session).Turns);
        Assert.Equal(Enumerable.Range(9, 12), turns.Select(turn => turn.Index));
        Assert.All(turns, turn =>
        {
            Assert.Equal(8, turn.Calls.Count);
            Assert.All(turn.Calls, call => Assert.DoesNotContain("hunter", call, StringComparison.Ordinal));
            Assert.All(turn.Calls, call => Assert.True(call.Length <= 200 + "…[truncated]".Length));
            Assert.DoesNotContain("hunter", turn.Outcome, StringComparison.Ordinal);
            Assert.True(turn.Outcome.Length <= 2048 + "…[truncated]".Length);
        });
    }

    [Fact]
    public async Task The_tool_loop_redacts_a_secret_in_a_tool_result_before_it_cuts_the_result()
    {
        var model = new ScriptedModel(request => request.Messages.Any(message => message.Role == "tool")
            ? ModelResponses.Done("passed", "Done.")
            : ModelResponses.Call("echo", new { }));
        var executor = ToolLoop.CreateToolLoopExecutor(new ToolLoopExecutorOptions
        {
            Name = "echoer",
            BuildPrompt = context => Task.FromResult(context.Step.Instruction),
            Tools = (_, _) =>
            [
                new ToolLoopTool(
                    new ModelTool { Name = "echo", Description = "Echo", Parameters = System.Text.Json.JsonDocument.Parse("""{ "type": "object" }""").RootElement.Clone() },
                    (_, _) => Task.FromResult(new ToolLoopResult(new string('r', 593) + "hunter2xyz"))),
            ],
        });
        await using var session = await StartAsync(executor, model);

        await session.Agent.ActAsync(
            "echo",
            new ActOptions { Params = new Dictionary<string, object?> { ["pw"] = Secret.Create("pw", "hunter2xyz", "password") } });

        var turns = Assert.IsAssignableFrom<IReadOnlyList<StepTurn>>(AgentStep(session).Turns);
        Assert.DoesNotContain("hunt", turns[0].Outcome, StringComparison.Ordinal);
    }

    [Fact]
    public void Truncate_never_splits_a_surrogate_pair()
    {
        // "😀" is two UTF-16 characters; a cut after the first would leave a lone high surrogate.
        Assert.Equal("ab…[truncated]", TurnLog.Truncate("ab😀cd", 3));
        Assert.Equal("ab😀…[truncated]", TurnLog.Truncate("ab😀cd", 4));
        Assert.Equal("ab😀", TurnLog.Truncate("ab😀", 4));
    }

    private static StepRecord AgentStep(E2ESession session) => session.StepRecords.Single(step => step.Api == "agent.act");

    private static Task<E2ESession> StartAsync(IStepExecutor? executor, IAgentModel? model = null) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            AgentExecutor = executor,
            Model = model,
            BaseUrl = "https://counter.test",
            TestTitle = "turns",
        });

    private static Task<E2ESession> StartAsync(IStepExecutor executor) => StartAsync(executor, null);
}
