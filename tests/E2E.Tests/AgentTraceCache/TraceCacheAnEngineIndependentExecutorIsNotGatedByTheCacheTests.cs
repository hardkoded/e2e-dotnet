// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentTraceCache;

public sealed class TraceCacheAnEngineIndependentExecutorIsNotGatedByTheCacheTests
{
    private const string Instruction = "do the work with your own tools";

    private static string NewDirectory() => Path.Combine(Path.GetTempPath(), "e2e-engine-independent", Guid.NewGuid().ToString("n"));

    private static Task<E2ESession> StartAsync(IEngine engine, IStepExecutor executor, string directory) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            AgentExecutor = executor,
            BaseUrl = "https://counter.test",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = "tools-only step",
        });

    [Fact]
    public async Task Skips_the_end_state_observation_when_the_executor_recorded_no_actions()
    {
        var directory = NewDirectory();
        var executorFinished = false;
        var observationsAfterStep = 0;
        var engine = new ObservingEngine(CounterPage.World(), () =>
        {
            if (executorFinished)
            {
                Interlocked.Increment(ref observationsAfterStep);
            }
        });
        var executor = new TestExecutor("observation-only-executor", async (context, _) =>
        {
            await context.ObserveAsync();
            executorFinished = true;
            return Passed("the screen already matches");
        });
        await using var session = await StartAsync(engine, executor, directory);
        await session.App.OpenAsync("/");

        await session.Agent.ActAsync(Instruction);
        session.Complete();

        Assert.True(engine.Observations > 0);
        Assert.True(executorFinished);
        Assert.Equal(0, observationsAfterStep);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task Runs_the_executor_with_caching_on_without_an_opened_app_and_stages_nothing()
    {
        var directory = NewDirectory();
        var calls = 0;
        var prefixes = new List<ReplayedPrefix?>();
        // Never observes, never acts: the shape of an executor that only uses its own tools.
        var executor = new TestExecutor("tools-only-executor", (context, _) =>
        {
            calls++;
            prefixes.Add(context.ReplayedPrefix);
            return Task.FromResult(Passed("done without the screen"));
        })
        {
            Version = "test",
        };
        await using var session = await StartAsync(new DocumentEngine(CounterPage.World()), executor, directory);

        await session.Agent.ActAsync(Instruction);
        session.Complete();

        Assert.Equal(1, calls);
        Assert.Equal([null], prefixes);
        Assert.False(Directory.Exists(directory));
    }

    /// <summary>A document engine that counts, and tells the test about, every observation it makes.</summary>
    private sealed class ObservingEngine(DocumentWorld world, Action onObserve) : IEngine
    {
        private readonly DocumentEngine _inner = new(world);
        private readonly Action _onObserve = onObserve;
        private int _observations;

        public int Observations => Volatile.Read(ref _observations);

        public string Platform => _inner.Platform;

        public string Version => _inner.Version;

        public EngineCapabilities Capabilities => _inner.Capabilities;

        public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            new Session(this, await _inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

        private sealed class Session(ObservingEngine engine, IEngineSession inner) : IEngineSession
        {
            public string Route => inner.Route;

            public Task OpenAsync(string url, CancellationToken cancellationToken) => inner.OpenAsync(url, cancellationToken);

            public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref engine._observations);
                engine._onObserve();
                return inner.ObserveAsync(cancellationToken);
            }

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => inner.PerformAsync(node, action, cancellationToken);

            public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

            public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => inner.SwipeAsync(direction, cancellationToken);

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
