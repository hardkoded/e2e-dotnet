// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.FixtureRuntime;

/// <summary>
/// Project tools under the step's budget and queue. Not ported, because the port has no step record: the events of the step, which
/// upstream reads for the tool's call and for the status of a failed tool. The slots a step used are read from its
/// <see cref="ActResult"/>.
/// </summary>
public sealed class ProjectToolDispatchTests
{
    private static readonly JsonObject EmptySchema = new() { ["type"] = "object", ["properties"] = new JsonObject() };

    /// <summary>Partial: the port reads the slot count of the step from the tool bodies that ran, since a failed step has no result.</summary>
    [Fact]
    public async Task Partial_Reserves_the_only_action_slot_before_parallel_model_tool_calls_can_mutate()
    {
        var started = 0;
        var turns = 0;
        var model = new ScriptedModel(_ => turns++ == 0
            ? new ModelResponse
            {
                ToolCalls =
                [
                    new ModelToolCall { Id = "call_1", Name = "mutate", Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { }) },
                    new ModelToolCall { Id = "call_2", Name = "mutate", Arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { }) },
                ],
            }
            : ModelResponses.Done("passed", "finished"));
        var tools = new Dictionary<string, AgentTool>
        {
            ["mutate"] = new AgentTool("changes the app", EmptySchema, true, async (_, _) =>
            {
                Interlocked.Increment(ref started);
                await Task.Delay(5);
                return "done";
            }),
        };

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("perform one mutation", new ActOptions { MaxSteps = 1 });
            },
            CounterPage.World(),
            model,
            agentTools: tools);

        Assert.Equal("STEP_BUDGET_EXHAUSTED", Assert.IsType<AgentException>(result.Error).Code);
        Assert.Equal(1, started);
    }

    /// <summary>Partial: the port has no tool event, so it reads the failed tool from the exception it threw and the step's actions from its result.</summary>
    [Fact]
    public async Task Partial_Serializes_grammar_actions_with_project_mutations_and_records_failed_tools_accurately()
    {
        var order = new List<string>();
        var world = new DocumentWorld().Map("/", page => page.Button("Go", () => order.Add("tap")));
        var executor = new TestExecutor("test", async (context, _) =>
        {
            var observation = await context.ObserveAsync();
            var tool = context.Budgets.RunToolAsync<string>("mutation", true, async () =>
            {
                order.Add("start");
                await Task.Delay(5);
                order.Add("end");
                throw new InvalidOperationException("failed mutation");
            });
            var tap = context.Actions.TapAsync(TargetFor(observation.Text, "button \"Go\""));
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => tool);
            Assert.Equal("failed mutation", failure.Message);
            await tap;
            return Passed("checked");
        });
        ActResult? act = null;

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                act = await ctx.Agent.ActAsync("sequence");
            },
            world,
            executor: executor);

        Assert.Null(result.Error);
        Assert.Equal(["start", "end", "tap"], order);
        Assert.Equal(2, act!.Actions);
    }

    [Fact]
    public async Task Gives_read_only_tools_the_guarded_observation_capability()
    {
        string? path = null;
        string? withheld = null;
        var turns = 0;
        var model = new ScriptedModel(_ => turns++ == 0 ? ModelResponses.Call("inspect", new { }) : ModelResponses.Done("passed", "looked"));
        var tools = new Dictionary<string, AgentTool>
        {
            ["inspect"] = new AgentTool("looks at the screen", EmptySchema, false, async (_, context, _) =>
            {
                var observation = await context.ObserveAsync(new ExecutorObserveOptions { Pixels = true });
                path = observation.Path;
                withheld = observation.PixelsWithheld?.ToString();
                return withheld ?? "";
            }),
        };

        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/settings/general");
                await ctx.Agent.ActAsync("inspect");
            },
            new DocumentWorld().Map("/settings/general", page => page.Paragraph("General")),
            model,
            agentTools: tools);

        Assert.Null(result.Error);
        Assert.Equal("/settings/general", path);
        Assert.Equal(nameof(VisionDegradation.UnsupportedCapability), withheld);
    }

    [Fact]
    public async Task Hands_an_executor_the_engines_measured_keyboard_state_and_nothing_when_the_engine_measured_none()
    {
        var measured = new List<bool?>();
        var engine = new KeyboardEngine(new DocumentEngine(CounterPage.World()));
        var executor = new TestExecutor("test", async (context, _) =>
        {
            measured.Add((await context.ObserveAsync()).KeyboardVisible);
            return Passed("looked");
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            AgentExecutor = executor,
            BaseUrl = "https://counter.test",
            TestTitle = "keyboard",
        });
        await session.App.OpenAsync("/");

        foreach (var state in new bool?[] { null, true, false })
        {
            engine.Reported = state;
            await session.Agent.ActAsync("look");
        }

        session.Complete();
        Assert.Equal([null, true, false], measured);
    }

    /// <summary>An engine over another that reports the keyboard state a test sets on every observation.</summary>
    private sealed class KeyboardEngine(IEngine inner) : IEngine
    {
        public bool? Reported { get; set; }

        public string Platform => inner.Platform;

        public string Version => inner.Version;

        public EngineCapabilities Capabilities => inner.Capabilities;

        public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            new Session(this, await inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

        private sealed class Session(KeyboardEngine engine, IEngineSession session) : IEngineSession
        {
            public string Route => session.Route;

            public Task OpenAsync(string url, CancellationToken cancellationToken) => session.OpenAsync(url, cancellationToken);

            public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
            {
                var observation = await session.ObserveAsync(cancellationToken).ConfigureAwait(false);
                return new Observation
                {
                    Route = observation.Route,
                    Roots = observation.Roots,
                    Truncated = observation.Truncated,
                    KeyboardVisible = engine.Reported,
                };
            }

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => session.PerformAsync(node, action, cancellationToken);

            public Task PressAsync(string key, CancellationToken cancellationToken) => session.PressAsync(key, cancellationToken);

            public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => session.SwipeAsync(direction, cancellationToken);

            public Task BackAsync(CancellationToken cancellationToken) => session.BackAsync(cancellationToken);

            public Task RestartAsync(CancellationToken cancellationToken) => session.RestartAsync(cancellationToken);

            public Task ClearStateAsync(CancellationToken cancellationToken) => session.ClearStateAsync(cancellationToken);

            public ValueTask DisposeAsync() => session.DisposeAsync();
        }
    }
}
