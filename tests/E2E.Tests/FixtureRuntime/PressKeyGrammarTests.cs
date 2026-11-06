// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.FixtureRuntime;

/// <summary>
/// Upstream's <c>describe('press key grammar')</c> in <c>fixture-runtime.test.ts</c>, over an engine that declares the <c>tap</c> and
/// <c>press</c> actions and records every located expression and performed action.
/// </summary>
public sealed class PressKeyGrammarTests
{
    private static readonly SemanticNode Field = new() { Ref = "field", Role = "textbox", Name = "Name" };

    [Fact]
    public async Task Rejects_a_key_outside_the_grammar_in_the_locator_tier_before_the_locator_is_resolved()
    {
        var engine = new KeyboardEngine();
        await using var session = await StartAsync(engine);
        foreach (var key in new[] { "Ctrl+a", "Enter+Shift", "", "ab", "Shift" })
        {
            var error = await Assert.ThrowsAsync<TestException>(() => session.Screen.GetByLabel("Name").PressAsync(key));
            Assert.Equal("INVALID_ARGUMENT", error.Code);
            Assert.Contains($"press key \"{key}\" is not a key", error.Message, StringComparison.Ordinal);
        }

        Assert.Equal(0, engine.Locates);
        Assert.Empty(engine.Performed);
        await session.Screen.GetByLabel("Name").PressAsync("Control+a");
        await session.Screen.GetByLabel("Name").PressAsync("Shift++");
        Assert.Equal(["press:Control+a", "press:Shift++"], engine.Performed);
    }

    [Fact]
    public async Task Rejects_a_key_outside_the_grammar_in_the_agent_dispatcher_before_any_engine_call()
    {
        var engine = new KeyboardEngine();
        TestException? rejected = null;
        var executor = new TestExecutor("test", async (context, token) =>
        {
            await context.ObserveAsync(cancellationToken: token);
            try
            {
                await context.Actions.PressAsync(new ExecutorTarget("field"), "Ctrl+a", token);
            }
            catch (TestException error)
            {
                rejected = error;
            }

            await context.Actions.PressAsync(new ExecutorTarget("field"), "Shift+Tab", token);
            return Passed("pressed");
        })
        {
            Version = "1",
        };
        await using var session = await StartAsync(engine, executor);

        await session.Agent.ActAsync("press keys");

        Assert.NotNull(rejected);
        Assert.Equal("INVALID_ARGUMENT", rejected.Code);
        Assert.Contains("\"Ctrl+a\" is not a key", rejected.Message, StringComparison.Ordinal);
        Assert.Equal(["press:Shift+Tab"], engine.Performed);
    }

    [Fact]
    public async Task Fails_an_undeclared_action_kind_with_UNSUPPORTED_CAPABILITY_before_the_locator_is_resolved()
    {
        var engine = new KeyboardEngine();
        await using var session = await StartAsync(engine);

        var check = await Assert.ThrowsAsync<EngineException>(() => session.Screen.GetByLabel("Name").CheckAsync());
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, check.Code);
        Assert.Equal("the \"check\" action is not available on this target: its engine declares tap, press", check.Message);
        var swipe = await Assert.ThrowsAsync<EngineException>(() => session.Screen.SwipeAsync(new SwipeOptions { Direction = ScrollDirection.Down }));
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, swipe.Code);
        Assert.Equal(0, engine.Locates);
        Assert.Empty(engine.Performed);
    }

    private static Task<E2ESession> StartAsync(IEngine engine, IStepExecutor? executor = null) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            AgentExecutor = executor,
            BaseUrl = "https://app.test",
            TestTitle = "press key grammar",
            CacheEnabled = false,
        });

    /// <summary>Upstream's <c>keyboard()</c>: an engine that records every located expression and performed action.</summary>
    private sealed class KeyboardEngine : IEngine
    {
        private int _locates;

        public List<string> Performed { get; } = [];

        public int Locates => Volatile.Read(ref _locates);

        public string Platform => "fake";

        public string Version => "1";

        public EngineCapabilities Capabilities => EngineCapabilities.Observation | EngineCapabilities.Actions | EngineCapabilities.Location | EngineCapabilities.Keyboard;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            Task.FromResult<IEngineSession>(new Session(this));

        private sealed class Session(KeyboardEngine engine) : IEngineSession
        {
            public string Route => "/";

            public IReadOnlyCollection<string> ActionKinds => ["tap", "press"];

            public bool CanLocate => true;

            public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new Observation { Route = "/", Roots = [Field] });

            public Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorExpression expression, TimeSpan timeout, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref engine._locates);
                return Task.FromResult<IReadOnlyList<SemanticNode>>([Field]);
            }

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
            {
                engine.Performed.Add(action is LocatorAction.Press press ? "press:" + press.Key : EngineActionKinds.Of(action));
                return Task.CompletedTask;
            }

            public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
