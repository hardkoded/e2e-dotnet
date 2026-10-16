// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.Agent;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.FixtureRuntime;

/// <summary>
/// The screenshot the engine delivers after an executor's verdict. The port has no step record, so it reads the file the
/// attempt kept and the engine's own count, where upstream reads the step's artifacts and the artifact registry.
/// </summary>
public sealed class AssertEvidenceUnderACustomExecutorTests
{
    private static readonly TestExecutor Judging = new("judging", (_, _) => Task.FromResult(Passed("holds"))) { Version = "1" };

    [Fact]
    public async Task Attaches_the_screenshot_the_engine_delivers_after_the_verdict()
    {
        var root = TempRoot();
        var engine = new ScreenshotEngine(new DocumentWorld().Map("/", page => page.Paragraph("holds")), _ => ScreenshotEngine.Png());
        await using var session = await StartAsync(engine, root, TimeSpan.FromMilliseconds(300));
        await session.App.OpenAsync("/");

        await session.Agent.AssertAsync("the screen holds");
        session.Complete();

        Assert.Equal(1, engine.Screenshots);
        var kept = Directory.GetFiles(Path.Combine(root, ".e2e"), "*-assert.png", SearchOption.AllDirectories);
        Assert.Equal(ScreenshotEngine.Png(), await File.ReadAllBytesAsync(Assert.Single(kept)));
    }

    /// <summary>
    /// Partial: the port reads the time the assertion took from a clock, where upstream advances fake timers: the capture is
    /// abandoned at the action timeout, 300 ms here, long before the 20 s of the assertion's own timeout.
    /// </summary>
    [Fact]
    public async Task Partial_Abandons_a_screenshot_the_engine_never_delivers_at_the_operation_budget_not_the_step_timeout_and_keeps_the_verdict()
    {
        var root = TempRoot();
        var engine = new HungScreenshotEngine(new DocumentWorld().Map("/", page => page.Paragraph("holds")));
        await using var session = await StartAsync(engine, root, TimeSpan.FromMilliseconds(300));
        await session.App.OpenAsync("/");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        await session.Agent.AssertAsync("the screen holds", new AssertOptions { Timeout = TimeSpan.FromSeconds(20) });
        session.Complete();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "settled after " + watch.Elapsed);
        Assert.Equal(1, engine.Screenshots);
        Assert.False(Directory.Exists(Path.Combine(root, ".e2e")));
    }

    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "e2e-evidence-budget-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static Task<E2ESession> StartAsync(IEngine engine, string root, TimeSpan actionTimeout) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            AgentExecutor = Judging,
            BaseUrl = "https://app.test",
            ProjectRoot = root,
            TestTitle = "evidence budget",
            ActionTimeout = actionTimeout,
        });

    /// <summary>A document engine that takes a screenshot request and never answers it, whatever token it carries.</summary>
    private sealed class HungScreenshotEngine(DocumentWorld world) : IEngine
    {
        private readonly DocumentEngine _inner = new(world);
        private int _screenshots;

        public int Screenshots => Volatile.Read(ref _screenshots);

        public string Platform => _inner.Platform;

        public string Version => _inner.Version;

        public EngineCapabilities Capabilities => _inner.Capabilities | EngineCapabilities.Screenshot;

        public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            new Session(this, await _inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

        private sealed class Session(HungScreenshotEngine engine, IEngineSession inner) : IEngineSession
        {
            public string Route => inner.Route;

            public Task OpenAsync(string url, CancellationToken cancellationToken) => inner.OpenAsync(url, cancellationToken);

            public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => inner.ObserveAsync(cancellationToken);

            public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => inner.PerformAsync(node, action, cancellationToken);

            public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

            public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref engine._screenshots);
                return new TaskCompletionSource<EngineScreenshot>().Task;
            }

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
