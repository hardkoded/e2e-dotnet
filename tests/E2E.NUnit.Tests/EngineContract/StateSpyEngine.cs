// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Engine;

namespace E2E.NUnit.Tests.EngineContract;

/// <summary>
/// The stand-in for upstream's <c>createFakeEngine({ state })</c>: the web engine, with the state hooks recorded and, when asked, hidden
/// or failing. The engine runs for real, so a saved state is a real browser state.
/// </summary>
internal sealed class StateSpyEngine(IEngine inner, bool state = true, bool failRestore = false) : IEngine
{
    public List<EngineState> Captured { get; } = [];

    public List<EngineState> Restored { get; } = [];

    public int Started;

    public int Ended;

    public string Name => inner.Name;

    public string Platform => inner.Platform;

    public string Version => inner.Version;

    public EngineCapabilities Capabilities => state ? inner.Capabilities | EngineCapabilities.State : inner.Capabilities & ~EngineCapabilities.State;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        var session = await inner.StartAsync(options, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref Started);
        var proxy = DispatchProxy.Create<IBrowserSession, Spy>();
        ((Spy)(object)proxy).Setup(session, this, failRestore);
        return proxy;
    }

    public class Spy : DispatchProxy
    {
        private IEngineSession _session = null!;
        private StateSpyEngine _engine = null!;
        private bool _failRestore;

        public void Setup(IEngineSession session, StateSpyEngine engine, bool failRestore)
        {
            _session = session;
            _engine = engine;
            _failRestore = failRestore;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IEngineSession.RestoreStateAsync))
            {
                var restoring = (EngineState)args![0]!;
                _engine.Restored.Add(restoring);
                if (_failRestore)
                {
                    return Task.FromException(new EngineException("ENGINE_FAILURE", "cannot seed storage"));
                }
            }

            if (targetMethod.Name == nameof(IEngineSession.DisposeAsync))
            {
                Interlocked.Increment(ref _engine.Ended);
            }

            try
            {
                var result = targetMethod.Invoke(_session, args);
                if (targetMethod.Name == nameof(IEngineSession.CaptureStateAsync))
                {
                    return Capture((Task<EngineState>)result!);
                }

                return result;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private async Task<EngineState> Capture(Task<EngineState> task)
        {
            var captured = await task.ConfigureAwait(false);
            _engine.Captured.Add(captured);
            return captured;
        }
    }
}
