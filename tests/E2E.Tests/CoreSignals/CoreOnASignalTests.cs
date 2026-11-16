// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using E2E.Tests.ManagedProcess;

namespace E2E.Tests.CoreSignals;

/// <summary>
/// Upstream's <c>describe('@e2e-dev/core on a signal')</c> (<c>tests/integration/core-signals.test.ts</c>): a program that holds a host and gets SIGINT or SIGHUP
/// closes the host, and the engine with it, then ends by the signal, whatever the engine listens for; a close that fails says so on stderr before the process goes.
/// </summary>
public sealed class CoreOnASignalTests
{
    private const int SigHup = 1;
    private const int SigInt = 2;

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);

    [DllImport("libc.so.6", EntryPoint = "kill", SetLastError = true)]
    private static extern int KillLinux(int pid, int signal);

    // Runs a program that opens a host and a session, sends it the signal once it is ready, and reports how it ended.
    private static async Task<(int? Signal, int Code, string Stderr, bool Disposed)> Interrupt(bool failClose, int signal = SigInt, bool listens = false)
    {
        var dir = Directory.CreateTempSubdirectory("core-signals-").FullName;
        try
        {
            var info = new ProcessStartInfo(AppProgram.Executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = dir,
            };
            foreach (var arg in new[] { "signal-host", dir, failClose ? "fail-close" : "close", listens ? "listens" : "alone" })
            {
                info.ArgumentList.Add(arg);
            }

            using var child = Process.Start(info)!;
            try
            {
                var stderr = new StringBuilder();
                child.ErrorDataReceived += (_, line) =>
                {
                    lock (stderr)
                    {
                        stderr.AppendLine(line.Data);
                    }
                };
                child.BeginErrorReadLine();
                using var ready = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                while (await child.StandardOutput.ReadLineAsync(ready.Token) is { } line && !line.Contains("READY", StringComparison.Ordinal))
                {
                }

                Assert.False(child.HasExited, $"the program exited before it was ready:\n{stderr}");
                Assert.Equal(0, OperatingSystem.IsLinux() ? KillLinux(child.Id, signal) : Kill(child.Id, signal));

                // A program that listens for the signal itself decides when it ends: here it never does.
                using var stays = new CancellationTokenSource(TimeSpan.FromSeconds(listens ? 3 : 30));
                try
                {
                    await child.WaitForExitAsync(stays.Token);
                }
                catch (OperationCanceledException) when (listens)
                {
                    child.Kill();
                    await child.WaitForExitAsync();
                }

                child.WaitForExit();
                var killed = child.ExitCode == 128 + 9;
                lock (stderr)
                {
                    return (killed ? 9 : child.ExitCode > 128 ? child.ExitCode - 128 : null, child.ExitCode, stderr.ToString(), File.Exists(Path.Combine(dir, "disposed")));
                }
            }
            finally
            {
                // A wait that failed must not leave the program running.
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                    child.WaitForExit(TimeSpan.FromSeconds(10));
                }
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ClosesTheHostDisposingItsEngineThenEndsByTheSignal()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // The signals are POSIX; Windows has no `kill`.
        }

        var ended = await Interrupt(false);
        Assert.True(ended.Signal == SigInt && ended.Disposed, ended.Stderr);
        Assert.DoesNotContain("failed", ended.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_EndsBySighupThoughTheEngineListensForItAndLeavesAProgramThatListensItselfToDecide()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // The signals are POSIX; Windows has no `kill`.
        }

        var alone = await Interrupt(false, SigHup);
        Assert.True(alone.Signal == SigHup && alone.Disposed, alone.Stderr);
        var listening = await Interrupt(false, SigHup, listens: true);
        Assert.True(listening.Signal == 9 && listening.Disposed, listening.Stderr);
    }

    [Fact]
    public async Task SaysOnStderrThatACloseFailedAndStillEndsByTheSignal()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // The signals are POSIX; Windows has no `kill`.
        }

        var ended = await Interrupt(true);
        Assert.Equal(SigInt, ended.Signal);
        Assert.Contains("closing a host on SIGINT failed", ended.Stderr, StringComparison.Ordinal);
    }
}
