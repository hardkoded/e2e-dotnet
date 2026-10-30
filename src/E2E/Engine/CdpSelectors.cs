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
    public static async Task RegisterCdpSelectorsAsync(IBrowserContext context, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (timeout <= TimeSpan.Zero)
        {
            throw new EngineException(EngineErrorCodes.OperationTimeout, "CDP selector registration timed out", retryable: false);
        }

        var send = FindSend(context)
            ?? throw new EngineException(EngineErrorCodes.UnsupportedCapability, "this Playwright version cannot register selector engines in the CDP context; secure-field masks need them", retryable: false);
        foreach (var (name, source) in SelectorEngines.All)
        {
            var engine = new Dictionary<string, object> { ["name"] = name, ["source"] = "(" + source + ")(undefined)", ["contentScript"] = false };
            var arguments = new Dictionary<string, object> { ["selectorEngine"] = engine };
            try
            {
                Task pending;
                try
                {
                    pending = (Task)send.Invoke(context, ["registerSelectorEngine", arguments, false, (float?)timeout.TotalMilliseconds])!;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }

                await pending.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
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

    // The message channel is internal to Playwright's ChannelOwner, so it is reached by reflection, as upstream reaches _channel.
    private static MethodInfo? FindSend(IBrowserContext context)
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
                return send;
            }
        }

        return null;
    }
}
