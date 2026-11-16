// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace E2E;

/// <summary>
/// Ctrl-C for <see cref="Launch"/> hosts: on SIGINT, SIGTERM, or SIGHUP every host that asked for it (<see cref="LaunchOptions.HandleSignals"/>, on by default)
/// closes, so the browsers and device sessions it launched do not outlive the program. The signal then ends the program by its own disposition, unless the
/// program listens for it itself (a <see cref="PosixSignalRegistration"/> that sets <see cref="PosixSignalContext.Cancel"/>), in which case its listener decides.
/// Upstream's <c>core/signals.ts</c>.
/// </summary>
/// <remarks>
/// .NET-only difference: upstream raises the signal again once the hosts are closed, and its <c>engineListeners</c> marks the listeners an engine registers
/// while it opens. A .NET handler that leaves <see cref="PosixSignalContext.Cancel"/> false lets the runtime end the process by the signal after it returns,
/// so the handler closes the hosts and returns; and the runtime lists no registrations, so an engine's own listener cannot be told from the program's.
/// </remarks>
internal static class Signals
{
    private static readonly Lock Gate = new();

    private static readonly HashSet<Func<Task>> Closers = [];

    private static PosixSignalRegistration[]? s_registrations;

    /// <summary>Registers a host's close for the process signals; the returned action unregisters it. Upstream's <c>closeOnSignals</c>.</summary>
    public static Action CloseOnSignals(Func<Task> close)
    {
        lock (Gate)
        {
            Closers.Add(close);
            Install();
        }

        return () =>
        {
            lock (Gate)
            {
                Closers.Remove(close);
                if (Closers.Count == 0)
                {
                    Uninstall();
                }
            }
        };
    }

    /// <summary>Closes every registered host once; the runtime then ends the process by the signal, unless a listener of the program cancelled it. Upstream's <c>onSignal</c>.</summary>
    private static void OnSignal(PosixSignalContext context)
    {
        Func<Task>[] pending;
        lock (Gate)
        {
            // A second signal takes the default action at once, as upstream's does once its listeners are off.
            Uninstall();
            pending = [.. Closers];
            Closers.Clear();
        }

        var signal = context.Signal.ToString().ToUpperInvariant();
        var closes = pending.Select(close => CloseAsync(close, signal));
        Task.WhenAll(closes).GetAwaiter().GetResult();
    }

    private static async Task CloseAsync(Func<Task> close, string signal)
    {
        try
        {
            await close().ConfigureAwait(false);
        }
        catch (Exception cause)
        {
            // The process ends right after, so a close that failed is said on stderr or nowhere; the message is redacted first.
            Console.Error.Write($"@e2e-dev/core: closing a host on {signal} failed: {OpenSession.Redact(cause.Message)}\n");
        }
    }

    private static void Install()
    {
        if (s_registrations is not null)
        {
            return;
        }

        var created = new List<PosixSignalRegistration>();
        foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGHUP })
        {
            try
            {
                created.Add(PosixSignalRegistration.Create(signal, OnSignal));
            }
            catch (PlatformNotSupportedException)
            {
                // A platform without this signal has nothing to close on it.
            }
        }

        s_registrations = [.. created];
    }

    private static void Uninstall()
    {
        var registrations = s_registrations;
        s_registrations = null;
        if (registrations is null)
        {
            return;
        }

        foreach (var registration in registrations)
        {
            registration.Dispose();
        }
    }
}
