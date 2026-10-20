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

    private static StepRecord AgentStep(E2ESession session) => session.Steps.Single(step => step.Api == "agent.act");

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
