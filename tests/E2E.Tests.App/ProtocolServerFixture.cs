// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Engine.Protocol;
using E2E.Reports;

/// <summary>
/// Upstream's <c>packages/e2e/tests/fixtures/protocol-server.ts</c>: an engine server process for the protocol tests, a toy engine served
/// over stdio, which reports what it received on stderr so a test can read it off a failure message.
/// <c>initializationOptions.crashOnObserve</c> makes <c>observe</c> write to stderr and exit the process mid-request; <c>crashMode</c>
/// changes how:
/// <list type="bullet">
/// <item><c>stdout-first</c>: it closes its stdout 300 ms before it writes and exits.</item>
/// <item><c>late-child</c>: it exits, and a child that shares its stderr writes there 300 ms later.</item>
/// <item><c>stays-up</c>: it closes its stdout, writes, and never exits.</item>
/// <item><c>signal</c>: it writes and stops itself with SIGKILL.</item>
/// </list>
/// <c>hangDoctor</c> adds a <c>doctor</c> that logs the pid and never returns, in a process that ignores SIGTERM.
/// </summary>
internal static class ProtocolServerFixture
{
    private static Stream? s_stdout;

    // Kept for the process's life: a registration that is collected stops ignoring the signal.
    private static PosixSignalRegistration? s_sigterm;

    /// <summary>The fixture's <c>late-child</c> command: a copy of this program, which writes one line to the stderr it shares and exits.</summary>
    public const string LateChildCommand = "protocol-server-late-child";

    public static async Task RunAsync()
    {
        s_stdout = Console.OpenStandardOutput();
        await ProtocolServer.ServeEngineAsync(
            options =>
            {
                var crashOnObserve = options?["crashOnObserve"]?.GetValue<bool>() ?? false;
                var crashMode = options?["crashMode"]?.GetValue<string>();
                var hangDoctor = options?["hangDoctor"]?.GetValue<bool>() ?? false;
                var spec = new EngineSpec
                {
                    Name = "toy-server",
                    Version = "1.0.0",
                    SpiVersion = 1,
                    Platform = "toy",
                    Actions = ["tap"],
                    Observe = async (_, _) =>
                    {
                        if (crashOnObserve)
                        {
                            await CrashAsync(crashMode);
                        }

                        return new EngineSnapshot(
                            new EngineNode(
                                new NodeRef("root", ""),
                                "root",
                                null,
                                [new EngineNode(new NodeRef("n1", ""), "button", "Save")]
                            ),
                            new ViewportSize { Width = 100, Height = 100 }
                        )
                        {
                            Location = $"pid:{Environment.ProcessId}",
                        };
                    },
                    Perform = (_, _, _) => Task.CompletedTask,
                    Init = info =>
                    {
                        info.Log($"init slot {info.WorkerSlot}");
                        return Task.CompletedTask;
                    },
                    Doctor = hangDoctor
                        ? async info =>
                        {
                            // The server ignores SIGTERM, so only a kill stops it.
                            s_sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
                                context.Cancel = true
                            );
                            info.Log($"pid:{Environment.ProcessId}");
                            await Task.Delay(Timeout.Infinite);
                            return [];
                        }
                        : null,
                };
                return Task.FromResult(E2E.Engine.EngineManifest.DefineEngine(spec));
            },
            new MessageStreams(Console.OpenStandardInput(), s_stdout)
        );
    }

    /// <summary>The late child of <c>late-child</c>: writes its line to the shared stderr 300 ms after it started.</summary>
    public static async Task RunLateChildAsync()
    {
        await Task.Delay(300);
        await Console.Error.WriteLineAsync("toy server child: late line");
    }

    /// <summary>Upstream's <c>closeSync(1)</c>: closes the process's own stdout handle, since closing a .NET stream leaves it open.</summary>
    private static void CloseStdout()
    {
        // .NET's stdout stream owns a duplicate of descriptor 1: the pipe only ends once both are closed.
        s_stdout?.Dispose();
        if (OperatingSystem.IsWindows())
        {
            _ = CloseHandle(GetStdHandle(-11));
        }
        else
        {
            // The runtime keeps more descriptors on the pipe than 1: close each one that names the same pipe, so the client sees the end.
            var pipe = Directory.Exists("/proc/self/fd")
                ? File.ResolveLinkTarget("/proc/self/fd/1", false)?.FullName
                : null;
            if (pipe is not null)
            {
                foreach (var entry in Directory.GetFiles("/proc/self/fd"))
                {
                    if (
                        int.TryParse(Path.GetFileName(entry), out var fd)
                        && fd > 2
                        && File.ResolveLinkTarget(entry, false)?.FullName == pipe
                    )
                    {
                        _ = Close(fd);
                    }
                }
            }

            _ = Close(1);
        }
    }

    [DllImport("libc", EntryPoint = "close")]
    private static extern int Close(int fd);

    [DllImport("kernel32.dll")]
    private static extern nint GetStdHandle(int handle);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);

    /// <summary>Fails the request the way <paramref name="mode"/> says: the process exits, or, for <c>stays-up</c>, closes its stdout.</summary>
    private static async Task CrashAsync(string? mode)
    {
        if (mode is not (null or "stdout-first" or "late-child" or "stays-up" or "signal"))
        {
            throw new ArgumentException($"Unknown crashMode {mode}.", nameof(mode));
        }

        if (mode is "stdout-first" or "stays-up")
        {
            CloseStdout();
            await Task.Delay(300);
        }

        if (mode == "stays-up")
        {
            await Console.Error.WriteLineAsync("toy server: stdout closed, staying");
            await Task.Delay(Timeout.Infinite);
        }

        if (mode == "late-child")
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            {
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };
            start.ArgumentList.Add(LateChildCommand);
            Process.Start(start)?.Dispose();
        }

        await Console.Error.WriteLineAsync("toy server: crashing on purpose");
        if (mode == "signal")
        {
            Process.GetCurrentProcess().Kill();
            await Task.Delay(Timeout.Infinite);
        }

        Environment.Exit(7);
    }
}
