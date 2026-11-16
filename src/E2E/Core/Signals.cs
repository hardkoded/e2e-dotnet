// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace E2E;

/// <summary>
/// Ctrl-C for hosts: on SIGINT, SIGTERM, or SIGHUP every host that asked for it (<see cref="LaunchOptions.HandleSignals"/>, on by default) closes, so
/// the browsers and device sessions it launched do not outlive the program. The signal is then raised again for the process's own disposition. A
/// program that listens for the signal itself (<see cref="PosixSignalRegistration"/>) sees it again and decides. Upstream's <c>core/signals.ts</c>.
/// </summary>
/// <remarks>
/// Upstream marks the listeners an engine registers while it opens (<c>engineListeners</c>) and removes them before it raises the signal again. .NET
/// cannot list the registrations of a process, so an engine's listener that cancels a signal is not told apart from a program's.
/// </remarks>
internal static class Signals
{
    private static readonly PosixSignal[] SignalList = [PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGHUP];

    // How long the program's registrations get to cancel a signal raised again.
    private static readonly TimeSpan HandlersWait = TimeSpan.FromMilliseconds(500);

    private static readonly Lock Gate = new();

    private static readonly HashSet<Func<Task>> Closers = [];

    private static List<PosixSignalRegistration>? s_registrations;

    /// <summary>Registers a host's close for the process signals; the returned action unregisters it. Upstream's <c>closeOnSignals</c>.</summary>
    internal static Action CloseOnSignals(Func<Task> close)
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

    // Closes every registered host once, then hands the signal back to the process.
    private static void OnSignal(PosixSignalContext context)
    {
        context.Cancel = true;
        var signal = context.Signal;
        List<Func<Task>> closers;
        lock (Gate)
        {
            Uninstall();
            closers = [.. Closers];
            Closers.Clear();
        }

        _ = Task.Run(async () =>
        {
            // The process ends right after, so a close that failed is said on stderr or nowhere; its message is already redacted.
            await Task.WhenAll(closers.Select(close => CloseAsync(close, signal))).ConfigureAwait(false);
            Raise(signal);
        });
    }

    private static async Task CloseAsync(Func<Task> close, PosixSignal signal)
    {
        try
        {
            await close().ConfigureAwait(false);
        }
        catch (Exception cause)
        {
            await Console.Error.WriteAsync($"E2E: closing a host on {signal} failed: {cause.Message}\n").ConfigureAwait(false);
        }
    }

    private static void Raise(PosixSignal signal)
    {
        if (OperatingSystem.IsWindows())
        {
            Environment.Exit(128 + Number(signal));
            return;
        }

        // Our registration is gone: the signal goes through the program's own registrations. A probe keeps the context they all share; when none
        // of them cancelled it by the time they ran, the process takes the signal's default disposition and ends by it.
        var seen = new TaskCompletionSource<PosixSignalContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var probe = PosixSignalRegistration.Create(signal, context => seen.TrySetResult(context));
        try
        {
            Send(Number(signal));
            if (seen.Task.Wait(HandlersWait) && seen.Task.Result.Cancel)
            {
                return;
            }

            Terminate(signal);
        }
        catch (Exception cause) when (cause is DllNotFoundException or EntryPointNotFoundException)
        {
            Environment.Exit(128 + Number(signal));
        }
    }

    private static void Terminate(PosixSignal signal)
    {
        try
        {
            ResetToDefault(Number(signal));
            Send(Number(signal));
        }
        catch (Exception cause) when (cause is DllNotFoundException or EntryPointNotFoundException)
        {
            Environment.Exit(128 + Number(signal));
        }
    }

    // PosixSignal values are the runtime's own constants, not the POSIX numbers the C library takes.
    private static int Number(PosixSignal signal) => signal switch
    {
        PosixSignal.SIGHUP => 1,
        PosixSignal.SIGINT => 2,
        _ => 15,
    };

    private static void Send(int signal) =>
        _ = OperatingSystem.IsLinux() ? KillLinux(Environment.ProcessId, signal) : Kill(Environment.ProcessId, signal);

    private static void ResetToDefault(int signal) =>
        _ = OperatingSystem.IsLinux() ? SignalLinux(signal, 0) : SignalDefault(signal, 0);

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);

    // "libc" is a linker script on Linux, not a library the loader opens.
    [DllImport("libc.so.6", EntryPoint = "kill", SetLastError = true)]
    private static extern int KillLinux(int pid, int signal);

    [DllImport("libc", EntryPoint = "signal", SetLastError = true)]
    private static extern nint SignalDefault(int signal, nint handler);

    [DllImport("libc.so.6", EntryPoint = "signal", SetLastError = true)]
    private static extern nint SignalLinux(int signal, nint handler);

    private static void Install()
    {
        if (s_registrations is not null)
        {
            return;
        }

        s_registrations = [];
        foreach (var signal in SignalList)
        {
            try
            {
                s_registrations.Add(PosixSignalRegistration.Create(signal, OnSignal));
            }
            catch (PlatformNotSupportedException)
            {
                // The platform has no such signal; the others still close the host.
            }
        }
    }

    private static void Uninstall()
    {
        if (s_registrations is null)
        {
            return;
        }

        foreach (var registration in s_registrations)
        {
            registration.Dispose();
        }

        s_registrations = null;
    }
}
