// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.WinApp;

/// <summary>
/// Translation of driver failures onto the engine error contract.
/// <list type="table">
/// <listheader><term>driver failure</term><description>Code</description></listheader>
/// <item><term>already an engine, test, or configuration error</term><description>passed through untouched</description></item>
/// <item><term>cancellation</term><description>CANCELLED</description></item>
/// <item><term><see cref="DriverFault.ElementVanished"/></term><description>NODE_STALE (retryable)</description></item>
/// <item><term><see cref="DriverFault.Offscreen"/>, <see cref="DriverFault.ValueRejected"/>, <see cref="DriverFault.ForegroundLost"/></term><description>NOT_ACTIONABLE</description></item>
/// <item><term><see cref="DriverFault.AppNotFound"/></term><description>INVALID_STATE, naming <c>app.open()</c></description></item>
/// <item><term><see cref="DriverFault.Launch"/></term><description>ENVIRONMENT_UNAVAILABLE</description></item>
/// <item><term>anything else</term><description>ENGINE_FAILURE with the text</description></item>
/// </list>
/// <c>OPERATION_TIMEOUT</c> is never produced here: in the contract it means the harness budget expired, which
/// <see cref="RunAsync{T}"/> already reports as cancelled.
/// </summary>
public static class WinAppErrors
{
    /// <summary>Translates an unexpected driver error at the contract boundary.</summary>
    public static Exception TranslateError(Exception cause, string operation)
    {
        ArgumentNullException.ThrowIfNull(cause);
        if (cause is E2EException)
        {
            return cause;
        }

        if (cause is OperationCanceledException)
        {
            return WinAppSupport.Cancelled($"{operation} cancelled");
        }

        var text = $"{operation} failed: {cause.Message}";
        if (cause is not WindowDriverException driver)
        {
            return WinAppSupport.Failure(text, cause);
        }

        return driver.Fault switch
        {
            DriverFault.ElementVanished => new EngineException(EngineErrorCodes.NodeStale, text, retryable: true, cause),
            DriverFault.Offscreen or DriverFault.ValueRejected or DriverFault.ForegroundLost => new EngineException(EngineErrorCodes.NotActionable, text, retryable: false, cause),
            DriverFault.AppNotFound => WinAppSupport.InvalidState($"{text}; the app is not running, call app.open()"),
            DriverFault.Launch => WinAppSupport.Unavailable(text, cause),
            _ => WinAppSupport.Failure(text, cause),
        };
    }

    /// <summary>Runs one driver call under a cancellation token and translates its failure: the one boundary every call crosses.</summary>
    public static async Task<T> RunAsync<T>(string label, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await work(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception cause)
        {
            throw TranslateError(cause, label);
        }
    }

    public static async Task RunAsync(string label, Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        await RunAsync(
            label,
            async token =>
            {
                await work(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }
}
