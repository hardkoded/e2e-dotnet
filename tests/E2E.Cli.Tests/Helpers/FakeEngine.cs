// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Cli.Tests.Helpers;

/// <summary>
/// An engine for the session host tests, as upstream's <c>createFakeEngine</c>: one button, a screen that can fail
/// or be held, and counts of the attempts it started and ended. It declares no scroll, history, or screenshot.
/// </summary>
internal sealed class FakeEngine : IEngine
{
    private int _started;
    private int _disposed;
    private int _performed;
    private int _observed;
    private readonly List<string> _operations = [];

    public string Platform => "web";

    public string Version => "1.0.0";

    public EngineCapabilities Capabilities =>
        EngineCapabilities.Observation | EngineCapabilities.Actions | EngineCapabilities.Location | EngineCapabilities.Keyboard | Also
        | (Device ? EngineCapabilities.PinnedApp : 0);

    /// <summary>
    /// A device with a pinned app: it pins an app, opens no URL, and <c>RestartAsync</c> launches the app, which shows one button, "Greet".
    /// Upstream's <c>device</c> behavior of <c>createFakeEngine</c>.
    /// </summary>
    public bool Device { get; init; }

    /// <summary>The launches and looks at the screen of a device, in the order they reached the engine.</summary>
    public IReadOnlyList<string> Operations
    {
        get
        {
            lock (_operations)
            {
                return [.. _operations];
            }
        }
    }

    /// <summary>Capabilities the engine declares beyond the base four.</summary>
    public EngineCapabilities Also { get; init; }

    /// <summary>Runs when the engine starts an attempt; a throw fails the start.</summary>
    public Action? OnStart { get; init; }

    /// <summary>Runs on every look at the screen; a throw fails it.</summary>
    public Action? OnObserve { get; init; }

    /// <summary>Runs on every navigation; the navigation waits for the task it returns.</summary>
    public Func<Task>? OnNavigate { get; init; }

    /// <summary>Runs when an attempt's engine session is disposed; the dispose waits for the task it returns.</summary>
    public Func<Task>? OnDispose { get; init; }

    public int AttemptsStarted => Volatile.Read(ref _started);

    public int Disposes => Volatile.Read(ref _disposed);

    /// <summary>How many actions reached the engine.</summary>
    public int Actions => Volatile.Read(ref _performed);

    /// <summary>How many looks at the screen reached the engine.</summary>
    public int Observations => Volatile.Read(ref _observed);

    public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OnStart?.Invoke();
        Interlocked.Increment(ref _started);
        return Task.FromResult<IEngineSession>(new Session(this));
    }

    private void Record(string operation)
    {
        lock (_operations)
        {
            _operations.Add(operation);
        }
    }

    private sealed class Session(FakeEngine engine) : IEngineSession
    {
        private bool _launched;

        public string Route { get; private set; } = "/";

        public async Task OpenAsync(string url, CancellationToken cancellationToken)
        {
            if (engine.OnNavigate is { } navigate)
            {
                await navigate().WaitAsync(cancellationToken);
            }

            Route = Routes(url);
        }

        public Task RestartAsync(CancellationToken cancellationToken)
        {
            if (!engine.Device)
            {
                return Task.FromException(new EngineException("UNSUPPORTED_CAPABILITY", "this engine does not relaunch an app"));
            }

            engine.Record("restart");
            _launched = true;
            return Task.CompletedTask;
        }

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref engine._observed);
            engine.OnObserve?.Invoke();
            if (engine.Device)
            {
                engine.Record("observe");
                if (!_launched)
                {
                    throw new EngineException("APP_NOT_OPEN", "the app is not open");
                }

                return Task.FromResult(new Observation
                {
                    Route = Route,
                    Roots = [new SemanticNode { Ref = "k-0", Role = "root", Children = [new SemanticNode { Ref = "k-1", Role = "button", Name = "Greet" }] }],
                });
            }

            return Task.FromResult(new Observation
            {
                Route = Route,
                Roots =
                [
                    new SemanticNode
                    {
                        Ref = "k-0",
                        Role = "root",
                        Children =
                        [
                            new SemanticNode { Ref = "k-1", Role = "button", Name = "Submit" },
                            new SemanticNode { Ref = "k-2", Role = "textbox", Name = "Table" },
                        ],
                    },
                ],
            });
        }

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref engine._performed);
            return Task.CompletedTask;
        }

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public async ValueTask DisposeAsync()
        {
            if (engine.OnDispose is { } dispose)
            {
                await dispose();
            }

            Interlocked.Increment(ref engine._disposed);
        }

        private static string Routes(string url) => Uri.TryCreate(url, UriKind.Absolute, out var absolute) ? absolute.PathAndQuery : url;
    }
}
