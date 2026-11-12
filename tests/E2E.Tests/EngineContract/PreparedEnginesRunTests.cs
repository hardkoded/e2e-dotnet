// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Xunit;

namespace E2E.Tests.EngineContract;

/// <summary>
/// .NET-only: upstream's runner calls each target's <c>prepare</c> once per run and its <c>finish</c> after the last worker is gone (<c>run/provision.ts</c>).
/// The port's run path, <see cref="Runner.RunAsync"/>, does the same through <see cref="PreparedEngines"/>, once per engine instance, and the
/// run fixtures do it per process. These tests count the hooks over an engine whose session never starts, so no browser is needed.
/// </summary>
public sealed class PreparedEnginesRunTests
{
    [Fact]
    public async Task Prepares_a_declared_engine_once_for_every_test_of_the_run_and_finishes_it_when_the_run_ends()
    {
        var engine = new PreparingEngine();

        await RunTwoTestsAsync(engine, env: null);

        Assert.Equal(2, engine.StartCount);
        Assert.Equal(1, engine.Prepares);
        Assert.Equal(1, engine.Finishes);
    }

    [Fact]
    public async Task Starts_the_workers_with_the_prepared_environment_under_the_run_environment()
    {
        var engine = new PreparingEngine();
        engine.PreparedEnv["DEVICE_ID"] = "device-1";
        engine.PreparedEnv["SHARED"] = "prepared";

        await RunTwoTestsAsync(engine, new Dictionary<string, string?> { ["SHARED"] = "run", ["RUN_ONLY"] = "run" });

        Assert.Equal(2, engine.StartEnvs.Count);
        foreach (var env in engine.StartEnvs)
        {
            Assert.NotNull(env);
            Assert.Equal("device-1", env["DEVICE_ID"]);
            Assert.Equal("run", env["SHARED"]);
            Assert.Equal("run", env["RUN_ONLY"]);
        }
    }

    [Fact]
    public async Task A_failed_prepare_is_run_again_by_the_next_test_and_its_finish_is_still_called_once()
    {
        var engine = new PreparingEngine { FailPrepares = 1 };

        await RunTwoTestsAsync(engine, env: null);

        Assert.Equal(2, engine.Prepares);
        Assert.Equal(1, engine.StartCount);
        Assert.Equal(1, engine.Finishes);
    }

    /// <summary>
    /// The process's prepared engines, which the run fixtures finish in their teardown: a run that prepared an engine finishes it once, and a second
    /// finish has nothing left to release.
    /// </summary>
    [Fact]
    public async Task The_process_finishes_the_engines_its_fixtures_prepared_once_when_the_run_ends()
    {
        var engine = new PreparingEngine();
        var scope = new PrepareScope { RunId = "run", ProjectRoot = Directory.GetCurrentDirectory(), Env = PreparedEngines.ProcessEnvironment(), Notice = (_, _) => { } };
        await HostRun.Prepared.PrepareAsync(engine, "web", 1, scope, CancellationToken.None);

        await HostRun.FinishPreparedAsync();
        await HostRun.FinishPreparedAsync();

        Assert.Equal(1, engine.Prepares);
        Assert.Equal(1, engine.Finishes);
    }

    [Fact]
    public async Task A_shared_prepare_is_not_cancelled_by_the_caller_that_started_it()
    {
        var gate = new TaskCompletionSource();
        var engine = new PreparingEngine { Gate = gate.Task };
        var scope = new PrepareScope { RunId = "run", ProjectRoot = Directory.GetCurrentDirectory(), Env = PreparedEngines.ProcessEnvironment(), Notice = (_, _) => { } };
        var prepared = new PreparedEngines();
        using var starter = new CancellationTokenSource();

        var starting = prepared.PrepareAsync(engine, "web", 1, scope, starter.Token, shared: true);
        await starter.CancelAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => starting);

        var second = prepared.PrepareAsync(engine, "web", 1, scope, CancellationToken.None, shared: true);
        gate.SetResult();
        Assert.NotNull(await second);
        Assert.False(engine.HookSawCancel);
        Assert.Equal(1, engine.Prepares);

        await prepared.FinishAsync(new FinishScope { RunId = "run", Env = PreparedEngines.ProcessEnvironment(), CleanupTimeout = TimeSpan.FromSeconds(5), Notice = (_, _) => { }, OnFailure = _ => { } });
        Assert.Equal(1, engine.Finishes);
    }

    [Fact]
    public async Task The_run_shuts_its_workers_down_with_no_reuse_grant_before_it_finishes_the_engine()
    {
        var engine = new PreparingEngine();
        var scope = new PrepareScope { RunId = "run", ProjectRoot = Directory.GetCurrentDirectory(), Env = PreparedEngines.ProcessEnvironment(), Notice = (_, _) => { } };
        var prepared = new PreparedEngines();
        await prepared.PrepareAsync(engine, "web", 1, scope, CancellationToken.None);

        await prepared.FinishAsync(new FinishScope { RunId = "run", Env = PreparedEngines.ProcessEnvironment(), CleanupTimeout = TimeSpan.FromSeconds(5), Notice = (_, _) => { }, OnFailure = _ => { } });

        Assert.Equal(["prepare", "dispose", "finish"], engine.Order.ToArray());
        Assert.False(engine.DisposeGranted);
    }

    [Fact]
    public async Task A_dispose_that_fails_still_lets_the_engine_finish_and_release()
    {
        var engine = new PreparingEngine { DisposeFailure = new InvalidOperationException("close refused on purpose") };
        var scope = new PrepareScope { RunId = "run", ProjectRoot = Directory.GetCurrentDirectory(), Env = PreparedEngines.ProcessEnvironment(), Notice = (_, _) => { } };
        var prepared = new PreparedEngines();
        await prepared.PrepareAsync(engine, "web", 1, scope, CancellationToken.None);
        var failures = new List<Exception>();

        await prepared.FinishAsync(new FinishScope { RunId = "run", Env = PreparedEngines.ProcessEnvironment(), CleanupTimeout = TimeSpan.FromSeconds(5), Notice = (_, _) => { }, OnFailure = failures.Add });

        Assert.Equal(["prepare", "dispose", "finish"], engine.Order.ToArray());
        var failure = Assert.Single(failures);
        Assert.Contains("close refused on purpose", failure.Message);
    }

    [Fact]
    public async Task A_dispose_that_hangs_past_its_budget_is_reported_and_the_engine_still_finishes()
    {
        var engine = new PreparingEngine { DisposeHangs = true };
        var scope = new PrepareScope { RunId = "run", ProjectRoot = Directory.GetCurrentDirectory(), Env = PreparedEngines.ProcessEnvironment(), Notice = (_, _) => { } };
        var prepared = new PreparedEngines();
        await prepared.PrepareAsync(engine, "web", 1, scope, CancellationToken.None);
        var failures = new List<Exception>();

        await prepared.FinishAsync(new FinishScope { RunId = "run", Env = PreparedEngines.ProcessEnvironment(), CleanupTimeout = TimeSpan.FromSeconds(2), Notice = (_, _) => { }, OnFailure = failures.Add });

        Assert.Equal(["prepare", "dispose", "finish"], engine.Order.ToArray());
        var failure = Assert.IsType<EngineException>(Assert.Single(failures));
        Assert.Equal("CLEANUP_TIMEOUT", failure.Code);
    }

    private static async Task RunTwoTestsAsync(IEngine engine, IReadOnlyDictionary<string, string?>? env)
    {
        var project = Path.Combine(Path.GetTempPath(), "e2e-prepared-engines", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(project);
        try
        {
            await Runner.RunAsync(new RunOptions
            {
                Cwd = project,
                Tests = new InMemoryTests("tests/prepared.e2e", [new InMemoryTest("first", _ => Task.CompletedTask), new InMemoryTest("second", _ => Task.CompletedTask)]),
                Engine = engine,
                Env = env,
                // A CI run retries a failed test by default; the counts here are for one attempt each.
                RawConfig = """{ "retries": 0 }""",
            });
        }
        finally
        {
            Directory.Delete(project, recursive: true);
        }
    }

    /// <summary>
    /// A preparable engine that counts its hooks and records the environment each start is given. Its session never starts: a start throws, so the
    /// test attempt fails at its launch, and the counts are the whole observation.
    /// </summary>
    internal sealed class PreparingEngine : IPreparableEngine, IDisposableEngine
    {
        private readonly List<IReadOnlyDictionary<string, string?>?> _startEnvs = [];
        private int _prepares;
        private int _finishes;
        private int _starts;

        /// <summary>How many of the first prepare calls throw.</summary>
        public int FailPrepares { get; init; }

        /// <summary>When set, the prepare hook answers only after this task completes.</summary>
        public Task? Gate { get; init; }

        /// <summary>Whether the hook saw its token cancelled once it answered.</summary>
        public bool HookSawCancel { get; private set; }

        /// <summary>The hooks in the order they were called.</summary>
        public System.Collections.Concurrent.ConcurrentQueue<string> Order { get; } = new();

        /// <summary>Whether the dispose was granted a reuse signal.</summary>
        public bool DisposeGranted { get; private set; }

        /// <summary>What prepare leaves for the workers.</summary>
        public Dictionary<string, string> PreparedEnv { get; } = new(StringComparer.Ordinal);

        public int Prepares => Volatile.Read(ref _prepares);

        public int Finishes => Volatile.Read(ref _finishes);

        public int StartCount => Volatile.Read(ref _starts);

        public List<IReadOnlyDictionary<string, string?>?> StartEnvs
        {
            get
            {
                lock (_startEnvs)
                {
                    return [.. _startEnvs];
                }
            }
        }

        public string Platform => "web";

        public string Version => "1.0";

        public EngineCapabilities Capabilities => EngineCapabilities.None;

        public Task<EnginePrepareResult> PrepareAsync(EnginePrepareInfo info, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _prepares);
            Order.Enqueue("prepare");
            if (call <= FailPrepares)
            {
                throw new EngineException("ENGINE_FAILURE", "prepare refused on purpose");
            }

            return AfterGateAsync(cancellationToken);
        }

        private async Task<EnginePrepareResult> AfterGateAsync(CancellationToken cancellationToken)
        {
            if (Gate is not null)
            {
                await Gate.ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                HookSawCancel = true;
            }

            return new EnginePrepareResult { Env = PreparedEnv };
        }

        public Task FinishAsync(EngineFinishInfo info, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _finishes);
            Order.Enqueue("finish");
            return Task.CompletedTask;
        }

        /// <summary>When set, the dispose throws it.</summary>
        public Exception? DisposeFailure { get; init; }

        /// <summary>When set, the dispose never answers on its own: only its token ends it.</summary>
        public bool DisposeHangs { get; init; }

        public Task DisposeAsync(EngineDisposeContext context, CancellationToken cancellationToken)
        {
            Order.Enqueue("dispose");
            DisposeGranted = context.ReuseSignal is not null;
            if (DisposeFailure is not null)
            {
                return Task.FromException(DisposeFailure);
            }

            return DisposeHangs ? Task.Delay(Timeout.Infinite, cancellationToken) : Task.CompletedTask;
        }

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
        {
            lock (_startEnvs)
            {
                _startEnvs.Add(options.Env);
            }

            Interlocked.Increment(ref _starts);
            throw new EngineException("ENGINE_FAILURE", "the test engine starts no session");
        }
    }
}
