// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Tests.Core;

/// <summary>
/// An in-memory engine for the core tests: a scene of nodes, a saved state of <c>{ "ok": true }</c>, and hooks for the calls a test fails. Upstream's
/// <c>createFakeEngine</c> (<c>tests/helpers/fake-engine.ts</c>), with the <c>scene</c>, <c>state</c>, <c>onNavigate</c>, and <c>onCapture</c> options and
/// the <c>restoredStates</c>, <c>inits</c>, <c>attemptsStarted</c> and <c>disposes</c> counts the core tests read, and the <c>device</c> and <c>links</c> options:
/// a device engine pins an app and relaunches it, and one with links opens them, as <c>session.openLink</c> does.
/// </summary>
internal sealed class CoreFakeEngine : IEngine
{
    private readonly List<EngineState> _restored = [];
    private readonly List<(SemanticNode Node, LocatorAction Action)> _performed = [];
    private int _starts;
    private int _disposes;

    private readonly List<string> _openedLinks = [];
    private readonly List<(string Method, TimeSpan? Timeout)> _operations = [];

    public CoreFakeEngine(IReadOnlyList<SemanticNode>? scene = null, bool state = false, bool device = false, bool links = false)
    {
        Scene = scene ?? [new SemanticNode { Ref = "submit", Role = "button", Name = "Submit" }];
        Capabilities = EngineCapabilities.Observation
            | EngineCapabilities.Actions
            | (state ? EngineCapabilities.State : 0)
            | (device ? EngineCapabilities.PinnedApp : 0)
            | (links ? EngineCapabilities.OpenLink : 0);
    }

    /// <summary>The links the sessions opened through <c>OpenLinkAsync</c>, in order.</summary>
    public IReadOnlyList<string> OpenedLinks
    {
        get
        {
            lock (_openedLinks)
            {
                return [.. _openedLinks];
            }
        }
    }

    /// <summary>The engine calls that carry an operation, in order, with the timeout each was given. Upstream's <c>operations</c> (<c>timeoutMs</c>).</summary>
    public IReadOnlyList<(string Method, TimeSpan? Timeout)> Operations
    {
        get
        {
            lock (_operations)
            {
                return [.. _operations];
            }
        }
    }

    /// <summary>Throws when a session opens this URL's path; the message is the engine's own.</summary>
    public Func<string, Exception?>? OnNavigate { get; init; }

    /// <summary>Throws when a session captures its state.</summary>
    public Func<Exception?>? OnCapture { get; init; }

    public IReadOnlyList<SemanticNode> Scene { get; }

    public int Starts => Volatile.Read(ref _starts);

    public int Disposes => Volatile.Read(ref _disposes);

    public IReadOnlyList<EngineState> RestoredStates => _restored;

    public IReadOnlyList<(SemanticNode Node, LocatorAction Action)> Performed => _performed;

    public string Name => "fake";

    public string Platform => "fake";

    public string Version => "1";

    public EngineCapabilities Capabilities { get; }

    public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _starts);
        return Task.FromResult<IEngineSession>(new Session(this, options.ArtifactsDir));
    }

    private void Record(string method, OperationContext context)
    {
        lock (_operations)
        {
            _operations.Add((method, context.Timeout));
        }
    }

    private sealed class Session(CoreFakeEngine owner, string? artifactsDir) : IEngineSession
    {
        public string Route => "/";

        public Task OpenAsync(string url, OperationContext context, CancellationToken cancellationToken)
        {
            owner.Record("session.open", context);
            return OpenAsync(url, cancellationToken);
        }

        public Task RestartAsync(OperationContext context, CancellationToken cancellationToken)
        {
            owner.Record("session.restart", context);
            return Task.CompletedTask;
        }

        public Task OpenAsync(string url, CancellationToken cancellationToken) =>
            owner.OnNavigate?.Invoke(url) is { } failure ? Task.FromException(failure) : Task.CompletedTask;

        public Task OpenLinkAsync(string url, OperationContext context, CancellationToken cancellationToken)
        {
            owner.Record("session.openLink", context);
            lock (owner._openedLinks)
            {
                owner._openedLinks.Add(url);
            }

            return Task.CompletedTask;
        }

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => Task.FromResult(new Observation { Route = "/", Roots = owner.Scene });

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
        {
            lock (owner._performed)
            {
                owner._performed.Add((node, action));
            }

            return Task.CompletedTask;
        }

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string> ScreenshotArtifactAsync(string? label, CancellationToken cancellationToken)
        {
            var absolute = Path.Combine(artifactsDir!, "screenshots", "fake.png");
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllBytes(absolute, [0x89, 0x50, 0x4E, 0x47]);
            return Task.FromResult("screenshots/fake.png");
        }

        public Task<EngineState> CaptureStateAsync(CancellationToken cancellationToken) =>
            owner.OnCapture?.Invoke() is { } failure
                ? Task.FromException<EngineState>(failure)
                : Task.FromResult(new EngineState("fake-state", 1, new JsonObject { ["ok"] = true }));

        public Task RestoreStateAsync(EngineState state, CancellationToken cancellationToken)
        {
            lock (owner._restored)
            {
                owner._restored.Add(state);
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref owner._disposes);
            return ValueTask.CompletedTask;
        }
    }
}
