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

    // The fake engine's style reads, and the call it was given; upstream's `vi.fn(async () => ...)`.
    private sealed class StyleReads(Func<IReadOnlyDictionary<string, string>>? values = null)
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public (SemanticNode Node, IReadOnlyList<string> Properties, OperationContext Context)? Last { get; private set; }

        public Task<IReadOnlyDictionary<string, string>> Read(SemanticNode node, IReadOnlyList<string> properties, OperationContext context)
        {
            Interlocked.Increment(ref _calls);
            Last = (node, properties, context);
            return Task.FromResult(values?.Invoke() ?? new Dictionary<string, string> { ["color"] = "red" });
        }
    }

    private static Task<E2ESession> StartStyleSessionAsync(StyleEngine engine, IStepExecutor? executor = null, IAgentModel? model = null, IReadOnlyDictionary<string, AgentTool>? tools = null) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            AgentExecutor = executor,
            Model = model,
            AgentTools = tools,
            BaseUrl = "https://style.test",
            TestTitle = "style",
            CacheEnabled = false,
        });

    private static ExecutorReadOptions Style(params string[] properties) => new() { Style = properties };

    [Fact]
    public async Task Reads_only_requested_style_values_redacts_secrets_and_uses_no_action_slot()
    {
        const string sentinel = "style-secret-2718";
        var reads = new StyleReads(() => new Dictionary<string, string> { ["color"] = sentinel, ["opacity"] = "0.5", ["content"] = sentinel });
        ExecutorReadResult? result = null;
        var turns = 0;
        var model = new ScriptedModel(_ => turns++ == 0 ? ModelResponses.Call("inspect", new { }) : ModelResponses.Done("passed", "looked"));
        var tools = new Dictionary<string, AgentTool>
        {
            ["inspect"] = new AgentTool("looks at the screen", EmptySchema, false, async (_, context, token) =>
            {
                await context.ObserveAsync(cancellationToken: token);
                result = await context.ReadAsync(new ExecutorTarget("#message"), Style("color", "opacity"), token);
                return "inspected";
            }),
        };
        await using var session = await StartStyleSessionAsync(new StyleEngine { ReadStyle = reads.Read }, model: model, tools: tools);
        session.Register("member.password", sentinel);

        await session.Agent.ActAsync("inspect");

        session.Complete();
        Assert.Equal(new Dictionary<string, string> { ["color"] = "<secret:member.password>", ["opacity"] = "0.5" }, result!.Style);
        Assert.Equal(1, reads.Calls);
        var (node, properties, context) = reads.Last!.Value;
        Assert.Equal("message", node.Ref);
        Assert.Equal(["color", "opacity"], properties);
        Assert.Equal(OperationContext.Agent.Origin, context.Origin);
        var step = Assert.Single(session.Steps(), candidate => candidate.Api == "agent.act");
        Assert.Equal(0, step.Metrics!.ActionSteps);
        var read = Assert.Single(step.Events, entry => entry.Name == "readStyle");
        Assert.Equal((E2E.Reports.StepEventKind.Observation, E2E.Reports.StepEventStatus.Passed, "style: color, opacity"), (read.Kind, read.Status, read.Detail));
        Assert.DoesNotContain(sentinel, System.Text.Json.JsonSerializer.Serialize(session.Steps()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Denies_property_reads_inside_a_mutating_project_tool_before_reaching_the_engine()
    {
        var reads = new StyleReads();
        Exception? failure = null;
        var turns = 0;
        var model = new ScriptedModel(_ => turns++ == 0 ? ModelResponses.Call("inspect", new { }) : ModelResponses.Done("passed", "denied"));
        var tools = new Dictionary<string, AgentTool>
        {
            ["inspect"] = new AgentTool("changes the app", EmptySchema, true, async (_, context, token) =>
            {
                try
                {
                    await context.ReadAsync(new ExecutorTarget("message"), Style("color"), token);
                }
                catch (Exception cause)
                {
                    failure = cause;
                }

                return "done";
            }),
        };
        await using var session = await StartStyleSessionAsync(new StyleEngine { ReadStyle = reads.Read }, model: model, tools: tools);

        await session.Agent.ActAsync("inspect");

        session.Complete();
        Assert.Equal("POLICY_DENIED", Assert.IsType<AgentException>(failure).Code);
        Assert.Equal(0, reads.Calls);
    }

    [Fact]
    public async Task Refuses_missing_and_old_observation_ids_and_reads_behind_queued_observations()
    {
        var reads = new StyleReads();
        var failures = new List<Exception>();
        var engine = new StyleEngine
        {
            Nodes = generation => [new SemanticNode { Ref = "message-" + generation, Role = "status", Text = "Error" }],
            ReadStyle = reads.Read,
        };
        var executor = new TestExecutor("test", async (context, _) =>
        {
            var first = await context.ObserveAsync(new ExecutorObserveOptions { Tree = true });
            var old = first.Tree![0].Id;
            var fresh = context.ObserveAsync(new ExecutorObserveOptions { Tree = true });
            var pending = Task.Run(async () =>
            {
                try
                {
                    await context.ReadAsync(new ExecutorTarget(old), Style("color"));
                }
                catch (Exception cause)
                {
                    failures.Add(cause);
                }
            });
            var latest = await fresh;
            await pending;
            try
            {
                await context.ReadAsync(new ExecutorTarget("absent"), Style("color"));
            }
            catch (Exception cause)
            {
                failures.Add(cause);
            }

            await context.ReadAsync(new ExecutorTarget(latest.Tree![0].Id), Style("color"));
            return Passed("looked");
        });
        await using var session = await StartStyleSessionAsync(engine, executor);

        await session.Agent.ActAsync("inspect");

        session.Complete();
        Assert.Equal(["LOCATOR_NOT_FOUND", "LOCATOR_NOT_FOUND"], failures.Select(failure => Assert.IsType<E2EException>(failure, exactMatch: false).Code));
        Assert.Equal(1, reads.Calls);
    }

    [Fact]
    public async Task Denies_reads_on_an_engine_without_readStyle()
    {
        Exception? failure = null;
        var executor = new TestExecutor("test", async (context, _) =>
        {
            await context.ObserveAsync();
            try
            {
                await context.ReadAsync(new ExecutorTarget("message"), Style("color"));
            }
            catch (Exception cause)
            {
                failure = cause;
            }

            return Passed("looked");
        });
        await using var session = await StartStyleSessionAsync(new StyleEngine(), executor);

        await session.Agent.ActAsync("inspect");

        session.Complete();
        var denied = Assert.IsType<AgentException>(failure);
        Assert.Equal("POLICY_DENIED", denied.Code);
        Assert.Contains("computed styles", denied.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("before")]
    [InlineData("after")]
    public async Task Refuses_a_value_when_the_step_deadline_expires_around_the_engine_read(string expires)
    {
        var timeout = TimeSpan.FromMilliseconds(1_000);
        var reads = new StyleReads();
        Exception? failure = null;
        ExecutorReadResult? value = null;
        var engine = new StyleEngine
        {
            Nodes = _ => [new SemanticNode { Ref = "message", Role = "status" }],
            ReadStyle = async (node, properties, context) =>
            {
                if (expires == "after")
                {
                    await Task.Delay(timeout + TimeSpan.FromMilliseconds(100), CancellationToken.None);
                }

                return await reads.Read(node, properties, context);
            },
        };
        var executor = new TestExecutor("test", async (context, _) =>
        {
            await context.ObserveAsync();
            if (expires == "before")
            {
                await Task.Delay(timeout + TimeSpan.FromMilliseconds(100), CancellationToken.None);
            }

            try
            {
                value = await context.ReadAsync(new ExecutorTarget("message"), Style("color"));
            }
            catch (Exception cause)
            {
                failure = cause;
            }

            return Passed("looked");
        });
        await using var session = await StartStyleSessionAsync(engine, executor);

        var act = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("read within the deadline", new ActOptions { Timeout = timeout }));

        Assert.Equal("STEP_TIMEOUT", act.Code);
        Assert.Equal("STEP_TIMEOUT", Assert.IsType<AgentException>(failure).Code);
        Assert.Null(value);
        Assert.Equal(expires == "before" ? 0 : 1, reads.Calls);
        if (expires == "after")
        {
            var read = Assert.Single(session.Steps().Single(step => step.Api == "agent.act").Events, entry => entry.Name == "readStyle");
            Assert.Equal((E2E.Reports.StepEventStatus.Failed, "STEP_TIMEOUT"), (read.Status, read.Code));
        }

        session.Complete(act);
    }

    [Theory]
    [InlineData("operation timeout near the deadline", 50, "OPERATION_TIMEOUT", "STEP_TIMEOUT", "STEP_TIMEOUT")]
    [InlineData("stale node after the deadline", -50, "NODE_STALE", "STEP_TIMEOUT", "STEP_TIMEOUT")]
    [InlineData("operation timeout with time remaining", 1_000, "OPERATION_TIMEOUT", "ACTION_FAILED", "OPERATION_TIMEOUT")]
    [InlineData("stale node with time remaining", 1_000, "NODE_STALE", "LOCATOR_NOT_FOUND", "LOCATOR_NOT_FOUND")]
    public async Task Accounts_for_a_rejected_style_read(string label, int remainingMs, string code, string expected, string eventCode)
    {
        _ = label;
        var rejectsAt = DateTimeOffset.MinValue;
        Exception? failure = null;
        var engine = new StyleEngine
        {
            Nodes = _ => [new SemanticNode { Ref = "message", Role = "status" }],
            ReadStyle = async (_, _, _) =>
            {
                var wait = rejectsAt - DateTimeOffset.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, CancellationToken.None);
                }

                throw new EngineException(code, "style read failed", retryable: false);
            },
        };
        var executor = new TestExecutor("test", async (context, _) =>
        {
            await context.ObserveAsync();
            rejectsAt = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(context.Budgets.RemainingMs() - remainingMs);
            try
            {
                await context.ReadAsync(new ExecutorTarget("message"), Style("color"));
            }
            catch (Exception cause)
            {
                failure = cause;
            }

            return Passed("caught the read failure");
        });
        await using var session = await StartStyleSessionAsync(engine, executor);
        var act = session.Agent.ActAsync("handle a rejected style read", new ActOptions { Timeout = TimeSpan.FromMilliseconds(1_500) });

        Exception? thrown = null;
        try
        {
            await act;
        }
        catch (Exception cause)
        {
            thrown = cause;
        }

        Assert.Equal(expected == "STEP_TIMEOUT", thrown is AgentException { Code: "STEP_TIMEOUT" });
        Assert.Equal(expected, Assert.IsType<AgentException>(failure).Code);
        var read = Assert.Single(session.Steps().Single(step => step.Api == "agent.act").Events, entry => entry.Name == "readStyle");
        Assert.Equal((E2E.Reports.StepEventStatus.Failed, eventCode), (read.Status, read.Code));
        session.Complete(thrown);
    }

    [Fact]
    public async Task Fails_closed_when_the_engine_omits_a_requested_property()
    {
        Exception? failure = null;
        var engine = new StyleEngine { ReadStyle = new StyleReads(() => new Dictionary<string, string>()).Read };
        var executor = new TestExecutor("test", async (context, _) =>
        {
            await context.ObserveAsync();
            try
            {
                await context.ReadAsync(new ExecutorTarget("message"), Style("color"));
            }
            catch (Exception cause)
            {
                failure = cause;
            }

            return Passed("looked");
        });
        await using var session = await StartStyleSessionAsync(engine, executor);

        await session.Agent.ActAsync("inspect");

        session.Complete();
        var unreachable = Assert.IsType<AgentException>(failure);
        Assert.Equal("APP_UNREACHABLE", unreachable.Code);
        Assert.Contains("returned no string for \"color\"", unreachable.Message, StringComparison.Ordinal);
        var read = Assert.Single(session.Steps().Single(step => step.Api == "agent.act").Events, entry => entry.Name == "readStyle");
        Assert.Equal((E2E.Reports.StepEventStatus.Failed, "ENGINE_FAILURE"), (read.Status, read.Code));
    }

    [Fact]
    public async Task Preserves_complete_style_values_that_resemble_a_secret_prefix()
    {
        ExecutorReadResult? value = null;
        Exception? failure = null;
        var reads = new StyleReads(() => new Dictionary<string, string> { ["opacity"] = "0.5" });
        var executor = new TestExecutor("test", async (context, _) =>
        {
            await context.ObserveAsync();
            try
            {
                await context.ReadAsync(new ExecutorTarget("message"), Style("content"));
            }
            catch (Exception cause)
            {
                failure = cause;
            }

            value = await context.ReadAsync(new ExecutorTarget("message"), Style("opacity"));
            return Passed("looked");
        });
        await using var session = await StartStyleSessionAsync(new StyleEngine { ReadStyle = reads.Read }, executor);
        session.Register("prefix", "0.5abc");

        await session.Agent.ActAsync("inspect");

        session.Complete();
        Assert.Equal("INVALID_ARGUMENT", Assert.IsType<TestException>(failure).Code);
        Assert.Equal(1, reads.Calls);
        Assert.Equal(new Dictionary<string, string> { ["opacity"] = "0.5" }, value!.Style);
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

    [Fact]
    public async Task Gives_the_step_start_observation_the_same_view_as_an_observe()
    {
        var directory = Path.Combine(Path.GetTempPath(), "e2e-start-view", Guid.NewGuid().ToString("n"));
        ExecutorObservation? start = null;
        ExecutorObservation? observed = null;
        var engine = new KeyboardEngine(new DocumentEngine(new DocumentWorld().Map("/settings/general", page => page.Paragraph("General")))) { Reported = true, Truncated = true };
        var executor = new TestExecutor("test", async (context, _) =>
        {
            start = context.Start;
            observed = await context.ObserveAsync();
            return Passed("looked");
        });
        try
        {
            await using var session = await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = engine,
                AgentExecutor = executor,
                BaseUrl = "https://settings.test",
                Cache = new FileStepCache(directory),
                CacheEnabled = true,
                CacheMode = CacheMode.ReadWrite,
                TestTitle = "start view",
            });
            await session.App.OpenAsync("/settings/general");

            await session.Agent.ActAsync("look");

            session.Complete();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        Assert.NotNull(start);
        Assert.True(start.KeyboardVisible);
        Assert.True(start.Truncated);
        Assert.Equal("/settings/general", start.Path);
        Assert.Equal(observed!.Path, start.Path);
        Assert.Equal(observed.Truncated, start.Truncated);
        Assert.Equal(observed.Text, start.Text);
        Assert.Null(start.Tree);
    }

    /// <summary>An engine over another that reports the keyboard state a test sets on every observation.</summary>
    private sealed class KeyboardEngine(IEngine inner) : IEngine
    {
        public bool? Reported { get; set; }

        public bool Truncated { get; set; }

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
                    Truncated = observation.Truncated || engine.Truncated,
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
