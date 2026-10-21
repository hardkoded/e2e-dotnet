// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Agent;

/// <summary>
/// A device with a pinned app, over a document engine: it pins an app, opens no URL, and <c>RestartAsync</c> launches the app, which
/// opens the home route of the world. Upstream's <c>device</c> behavior of <c>createFakeEngine</c>. <see cref="Operations"/> lists the
/// restarts and looks at the screen in the order they reached the engine.
/// </summary>
internal sealed class DeviceEngine(DocumentWorld world) : IEngine
{
    private readonly ScreenshotEngine _inner = new(world, _ => ScreenshotEngine.Png());
    private readonly List<string> _operations = [];

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

    public string Platform => _inner.Platform;

    public string Version => _inner.Version;

    public EngineCapabilities Capabilities => _inner.Capabilities | EngineCapabilities.PinnedApp;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
        new Session(this, await _inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

    private void Record(string operation)
    {
        lock (_operations)
        {
            _operations.Add(operation);
        }
    }

    private sealed class Session(DeviceEngine engine, IEngineSession inner) : IEngineSession
    {
        public string Route => inner.Route;

        public Task OpenAsync(string url, CancellationToken cancellationToken) =>
            Task.FromException(new EngineException("UNSUPPORTED_CAPABILITY", "a device opens no URL"));

        public Task RestartAsync(CancellationToken cancellationToken)
        {
            engine.Record("restart");
            return inner.OpenAsync("/", cancellationToken);
        }

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            engine.Record("observe");
            return inner.ObserveAsync(cancellationToken);
        }

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => inner.PerformAsync(node, action, cancellationToken);

        public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

        public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) => inner.ScreenshotAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
