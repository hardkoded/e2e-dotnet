// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>Calls a target's <c>app.command.env</c> function (upstream's <c>commandEnvs</c> in <c>run/provision.ts</c>).</summary>
internal static class CommandEnvs
{
    private static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Calls the env function the config gave <paramref name="targetName"/>, with the services, and returns the variables it returned: checked like a
    /// static <c>env</c> object, with <c>{port}</c> expanded to the target URL's port. Null when the target has no env function. A config or
    /// infrastructure error the function throws keeps its code; any other error becomes <c>INVALID_CONFIG</c>, since the function is setup code. A
    /// function that does not return within the command's <c>startupTimeout</c> fails the start with <c>APP_UNREACHABLE</c>.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>?> ResolveAsync(
        E2EConfig config,
        string targetName,
        ResolvedApp app,
        ServiceValues services,
        CancellationToken cancellationToken
    )
    {
        if (
            !config.CommandEnvs.TryGetValue(targetName, out var env)
            || app.Command is not { } command
            || app.ReadyUrl is null
        )
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var label = "target \"" + targetName + "\" app.command";
        var timeout = command.StartupTimeout ?? DefaultStartupTimeout;

        // The function cannot be forced to stop, so the wait is bounded and a result that comes later is ignored.
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var call = Task.Run(() => env(services, waiting.Token), CancellationToken.None);
        IReadOnlyDictionary<string, string?>? returned;
        try
        {
            if (await Task.WhenAny(call, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false) != call)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await waiting.CancelAsync().ConfigureAwait(false);
                _ = call.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
                throw new EngineException(
                    "APP_UNREACHABLE",
                    label
                        + ".env didn't return within "
                        + ((long)timeout.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " ms"
                );
            }

            returned = await call.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception cause) when (cause is not EngineException { Code: "APP_UNREACHABLE" })
        {
            throw cause is E2EException and not TestException
                ? cause
                : new ConfigurationException("INVALID_CONFIG", label + ".env threw: " + cause.Message, cause);
        }

        return TargetApps.ReturnedEnv(returned, label, app.Base is { } baseUrl ? Urls.PortOf(baseUrl) : null);
    }
}
