// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// Wraps an engine and records the origin of every action it is asked to perform (.NET-only): what <c>makeEngine</c> in upstream's
/// <c>locator-engine.test.ts</c> records at its fake session, here at the real session a test's step reaches. An action that reaches the
/// session without a context is not recorded, so a caller that never says who acts fails the test.
/// </summary>
internal sealed class OriginRecordingEngine(IEngine inner) : IEngine
{
    private readonly List<OperationOrigin> _origins = [];

    public IReadOnlyList<OperationOrigin> Origins
    {
        get
        {
            lock (_origins)
            {
                return [.. _origins];
            }
        }
    }

    public string Platform => inner.Platform;

    public string Version => inner.Version;

    public EngineCapabilities Capabilities => inner.Capabilities;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
        new Session(this, await inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

    private sealed class Session(OriginRecordingEngine engine, IEngineSession inner) : IEngineSession
    {
        public string Route => inner.Route;

        public Task OpenAsync(string url, CancellationToken cancellationToken) => inner.OpenAsync(url, cancellationToken);

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => inner.ObserveAsync(cancellationToken);

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
            inner.PerformAsync(node, action, cancellationToken);

        public Task PerformAsync(SemanticNode node, LocatorAction action, OperationContext context, CancellationToken cancellationToken)
        {
            lock (engine._origins)
            {
                engine._origins.Add(context.Origin);
            }

            return inner.PerformAsync(node, action, cancellationToken);
        }

        public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
