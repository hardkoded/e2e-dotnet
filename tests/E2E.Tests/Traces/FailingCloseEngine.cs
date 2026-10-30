// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Engine;

namespace E2E.Tests.Traces;

/// <summary>
/// Test helper: stands for the fake engine of upstream's <c>engine-contract</c> and <c>skip-after-failure</c> tests whose
/// <c>endAttempt</c> throws. The port's <c>endAttempt</c> is the dispose of the engine's session, which here throws <paramref name="failure"/>.
/// </summary>
internal sealed class FailingCloseEngine(Exception failure) : IEngine
{
    public string Platform => "fake";

    public string Version => "test";

    public EngineCapabilities Capabilities => EngineCapabilities.Observation;

    public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        var session = DispatchProxy.Create<IEngineSession, SessionProxy>();
        ((SessionProxy)(object)session).Failure = failure;
        return Task.FromResult(session);
    }

    public class SessionProxy : DispatchProxy
    {
        internal Exception Failure { get; set; } = new InvalidOperationException();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            nameof(IAsyncDisposable.DisposeAsync) => ThrowFailure(),
            "get_" + nameof(IEngineSession.CanLocate) => false,
            _ => null,
        };

        private ValueTask ThrowFailure() => ValueTask.FromException(Failure);
    }
}
