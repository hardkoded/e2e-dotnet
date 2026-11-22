// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.OpenSession;

/// <summary>
/// An in-memory engine for the host tests: one button, one screenshot artifact named <c>fake.png</c>, and hooks for the calls a test holds or
/// fails. Upstream's <c>createFakeEngine</c> (<c>tests/helpers/fake-engine.ts</c>), which counts <c>inits</c>, <c>attemptsStarted</c>,
/// <c>attemptsEnded</c> and <c>disposes</c>: the port's engine has one start and one dispose for each session, so <see cref="Starts"/> and
/// <see cref="Disposes"/> are the counts.
/// </summary>
internal sealed class HostFakeEngine : IEngine
{
    private int _starts;
    private int _disposes;
    private readonly List<string> _calls = [];

    /// <summary>Runs inside every action; a wait on the token cancels with the session.</summary>
    public Func<LocatorAction, CancellationToken, Task>? OnPerform { get; init; }

    /// <summary>Runs when a session is disposed (upstream's <c>onEndAttempt</c>): a throw is a cleanup failure.</summary>
    public Func<Task>? OnDispose { get; init; }

    /// <summary>The calls the contributed <c>gadget</c> fixture ran (upstream's <c>fixtureCalls</c>): <c>slow</c> and <c>poke:&lt;what&gt;</c>.</summary>
    public IReadOnlyList<string> FixtureCalls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>The contributed <c>gadget</c> fixture of the last session started (upstream's <c>session.fixtures.gadget</c>).</summary>
    public Gadget? Gadget { get; private set; }

    public int Starts => Volatile.Read(ref _starts);

    public int Disposes => Volatile.Read(ref _disposes);

    /// <summary>What the last start was given.</summary>
    public EngineStartOptions? Started { get; private set; }

    public string Platform => "web";

    public string Version => "1";

    public EngineCapabilities Capabilities => EngineCapabilities.Observation | EngineCapabilities.Actions;

    public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _starts);
        Started = options;
        return Task.FromResult<IEngineSession>(new Session(this, options.ArtifactsDir));
    }

    private sealed class Session(HostFakeEngine owner, string? artifactsDir) : IEngineSession, IEngineFixtureHost
    {
        void IEngineFixtureHost.CreateFixtures(EngineFixtureContext context) =>
            owner.Gadget = new Gadget(context, call =>
            {
                lock (owner._calls)
                {
                    owner._calls.Add(call);
                }
            });

        public string Route => "/";

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(
                new Observation
                {
                    Route = "/",
                    Roots =
                    [
                        new SemanticNode
                        {
                            Ref = "submit",
                            Role = "button",
                            Name = "Submit",
                        },
                    ],
                }
            );

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
            owner.OnPerform?.Invoke(action, cancellationToken) ?? Task.CompletedTask;

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string> ScreenshotArtifactAsync(string? label, CancellationToken cancellationToken)
        {
            var absolute = Path.Combine(artifactsDir!, "screenshots", "fake.png");
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllBytes(absolute, [0x89, 0x50, 0x4E, 0x47]);
            return Task.FromResult("screenshots/fake.png");
        }

        public async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref owner._disposes);
            if (owner.OnDispose is { } fail)
            {
                await fail().ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// The contributed fixture of upstream's fake engine (<c>fixtures: true</c>): <c>poke</c>, <c>slow</c>, and <c>hang</c>, each a recorded
/// <c>resource</c> step of the session through the <see cref="EngineFixtureContext"/> the engine's session was given, as <c>Device</c> records its.
/// </summary>
internal sealed class Gadget(EngineFixtureContext context, Action<string> record)
{
    public Task<string> PokeAsync(string what) => context.RunOperationAsync(StepKind.Resource, "gadget.poke", "", () =>
        {
            record("poke:" + what);
            return Task.FromResult("poked " + what);
        });

    public Task<string> SlowAsync(TimeSpan? delay = null) =>
        context.RunOperationAsync(StepKind.Resource, "gadget.slow", "", async () =>
        {
            record("slow");
            await Task.Delay(delay ?? TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
            return "done";
        });

    public Task HangAsync() => context.RunOperationAsync(StepKind.Resource, "gadget.hang", "", async () =>
        {
            await new TaskCompletionSource().Task.ConfigureAwait(false);
            return true;
        });
}
