// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Engine;

namespace E2E.Tests.E2ESessionCleanup;

/// <summary>.NET-only: a session that fails to start after its engine started releases the engine session.</summary>
public sealed class E2ESessionCleanupTests
{
    [Fact]
    public async Task Disposes_the_started_engine_session_when_the_start_fails_afterwards()
    {
        var engine = new CountingEngine();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = engine,
            TestTitle = "cleanup > case",
            ResultsDirectory = "bad\0path",
        }));

        Assert.Equal(1, engine.Disposed);
    }

    private sealed class CountingEngine : IEngine
    {
        public int Disposed { get; set; }

        public string Platform => "document";

        public string Version => "test";

        public EngineCapabilities Capabilities => EngineCapabilities.Observation;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
        {
            var session = DispatchProxy.Create<IEngineSession, SessionProxy>();
            ((SessionProxy)(object)session).OnDispose = () => Disposed++;
            return Task.FromResult(session);
        }
    }

    public class SessionProxy : DispatchProxy
    {
        internal Action? OnDispose { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IAsyncDisposable.DisposeAsync))
            {
                OnDispose!();
                return default(ValueTask);
            }

            return null;
        }
    }
}
