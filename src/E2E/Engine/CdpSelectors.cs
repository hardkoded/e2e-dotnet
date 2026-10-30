// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>Registers the engine's selector engines in a CDP default context (upstream's <c>web/src/cdp-selectors.ts</c>); every secure-field mask needs them.</summary>
internal static class CdpSelectors
{
    /// <summary>
    /// Playwright seeds custom selectors only in newly created contexts, not the persistent context
    /// <c>ConnectOverCDPAsync</c> returns, and its public register API rejects an already registered name before reaching that
    /// context. This sends the same <c>registerSelectorEngine</c> message <c>Selectors.RegisterAsync</c> sends to a context
    /// (upstream <c>registerCdpSelectors</c>). A Playwright that cannot take the message fails the connection: every secure-field mask
    /// resolves through these engines.
    /// </summary>
    public static Task RegisterCdpSelectorsAsync(IBrowserContext context, TimeSpan timeout, CancellationToken cancellationToken) =>
        RegisterCdpSelectorsAsync(context, FindChannel(context), timeout, cancellationToken);

    /// <summary>
    /// Registers through <paramref name="channel"/>, the context's message channel (upstream reads <c>context._channel</c>); null
    /// is a context with none. .NET-only overload, so tests can stand in for the channel as upstream's test stubs <c>_channel</c>.
    /// </summary>
    internal static async Task RegisterCdpSelectorsAsync(IBrowserContext context, ISelectorChannel? channel, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (timeout <= TimeSpan.Zero)
        {
            throw new EngineException(EngineErrorCodes.OperationTimeout, "CDP selector registration timed out", retryable: false);
        }

        if (channel is null)
        {
            throw new EngineException(EngineErrorCodes.UnsupportedCapability, "this Playwright version cannot register selector engines in the CDP context; secure-field masks need them", retryable: false);
        }

        foreach (var (name, source) in SelectorEngines.All)
        {
            try
            {
                await channel.RegisterSelectorEngineAsync(name, "(" + source + ")(undefined)", contentScript: false, timeout, cancellationToken)
                    .WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                throw new EngineException(EngineErrorCodes.OperationTimeout, "CDP selector registration timed out", retryable: false, ex);
            }
            catch (PlaywrightException ex) when (ex.Message.EndsWith($"\"{name}\" selector engine has been already registered", StringComparison.Ordinal))
            {
                // A version that seeds CDP contexts already has our process-wide registration.
            }
        }
    }

    /// <summary>The context's <c>registerSelectorEngine</c> message channel (upstream's <c>SelectorChannel</c>); .NET-only seam over Playwright's internal send.</summary>
    internal interface ISelectorChannel
    {
        /// <summary>Sends <c>registerSelectorEngine</c> for one engine.</summary>
        Task RegisterSelectorEngineAsync(string name, string source, bool contentScript, TimeSpan timeout, CancellationToken cancellationToken);
    }

    // The message channel is internal to Playwright's ChannelOwner, so it is reached by reflection, as upstream reaches _channel.
    private static ReflectedChannel? FindChannel(IBrowserContext context)
    {
        for (var type = context.GetType(); type is not null; type = type.BaseType)
        {
            // The generic overload of the same name has the same parameters, so a lookup by signature is ambiguous.
            var send = type
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .FirstOrDefault(method => method.Name == "SendMessageToServerAsync" && !method.IsGenericMethod
                    && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual([typeof(string), typeof(Dictionary<string, object>), typeof(bool), typeof(float?)]));
            if (send is not null)
            {
                return new ReflectedChannel(context, send);
            }
        }

        return null;
    }

    private sealed class ReflectedChannel(IBrowserContext context, MethodInfo send) : ISelectorChannel
    {
        public Task RegisterSelectorEngineAsync(string name, string source, bool contentScript, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var engine = new Dictionary<string, object> { ["name"] = name, ["source"] = source, ["contentScript"] = contentScript };
            var arguments = new Dictionary<string, object> { ["selectorEngine"] = engine };
            try
            {
                return send.Invoke(context, ["registerSelectorEngine", arguments, false, (float?)timeout.TotalMilliseconds]) as Task
                    ?? throw new EngineException(EngineErrorCodes.UnsupportedCapability, "this Playwright version cannot register selector engines in the CDP context; secure-field masks need them", retryable: false);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
