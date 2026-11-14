// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Engine;

namespace E2E.Tests.AgentAct;

/// <summary>
/// Wraps an engine and declares tabs (.NET-only): upstream's toolset test runs the web engine, which has tabs, so the agent is offered
/// <c>switch_tab</c> and <c>close_tab</c>. The document engine has no tabs, so this adds the two capabilities and accepts the two verbs.
/// </summary>
internal sealed class TabsEngine(IEngine inner) : IEngine
{
    public string Name => inner.Name;

    public string Platform => inner.Platform;

    public string Version => inner.Version;

    public EngineCapabilities Capabilities => inner.Capabilities | EngineCapabilities.SwitchTab | EngineCapabilities.CloseTab;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        var session = await inner.StartAsync(options, cancellationToken).ConfigureAwait(false);
        var proxy = DispatchProxy.Create<IEngineSession, TabsSession>();
        ((TabsSession)(object)proxy).Inner = session;
        return proxy;
    }

    public class TabsSession : DispatchProxy
    {
        public IEngineSession Inner { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name is nameof(IEngineSession.SwitchTabAsync) or nameof(IEngineSession.CloseTabAsync))
            {
                return Task.CompletedTask;
            }

            try
            {
                return targetMethod.Invoke(Inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
