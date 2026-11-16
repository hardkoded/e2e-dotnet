// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
using E2E.Tests.ManagedProcess;

namespace E2E.Tests.CoreSignals;

/// <summary>
/// Upstream's <c>describe('@e2e-dev/core on a signal')</c> (<c>tests/integration/core-signals.test.ts</c>): a program that holds a host and gets SIGINT or
/// SIGHUP closes the host, and the engine with it, then ends by the signal; a close that fails says so on stderr before the process goes. The program is
/// <c>E2E.Tests.App core-signals</c>, which launches the fake engine of the core tests.
/// </summary>
public sealed class CoreOnASignalTests : IDisposable
{
    private const int SigHup = 1;

    private const int SigInt = 2;

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-core-signals-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Closes_the_host_disposing_its_engine_then_ends_by_the_signal()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var ended = await Interrupt(failClose: false, SigInt);
        Assert.True(ended.Signal == SigInt && ended.Disposed, ended.Stderr);
        Assert.DoesNotContain("failed", ended.Stderr);
    }

    // Partial: upstream's engine also listens for SIGHUP, as a browser driver does, and the host leaves that listener out of its decision; the port cannot
    // list the registrations of a process, so an engine's listener is not told from the program's (see COMPATIBILITY.md).
    [Fact]
    public async Task Partial_ends_by_SIGHUP_and_leaves_a_program_that_listens_itself_to_decide()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var ended = await Interrupt(failClose: false, SigHup);
        Assert.True(ended.Signal == SigHup && ended.Disposed, ended.Stderr);
        var listening = await Interrupt(failClose: false, SigHup, listens: true);
        Assert.True(listening.Killed && listening.Disposed, listening.Stderr);
    }

    [Fact]
    public async Task Says_on_stderr_that_a_close_failed_and_still_ends_by_the_signal()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var ended = await Interrupt(failClose: true, SigInt);
        Assert.Equal(SigInt, ended.Signal);
        Assert.Contains("@e2e-dev/core: closing a host on SIGINT failed", ended.Stderr);
        Assert.True(ended.Disposed, ended.Stderr);
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int signal);

    /// <summary>Runs a program that opens a host and a session, sends it <paramref name="signal"/> once it is ready, and reports how it ended.</summary>
    private async Task<Ended> Interrupt(bool failClose, int signal, bool listens = false)
    {
        var marker = Path.Combine(_dir, Guid.NewGuid().ToString("N"));
        var info = new ProcessStartInfo(AppProgram.Executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { "core-signals", marker, failClose ? "fail-close" : "-", listens ? "listens" : "-" })
        {
            info.ArgumentList.Add(arg);
        }

        using var child = Process.Start(info)!;
        var stderr = child.StandardError.ReadToEndAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var line = await child.StandardOutput.ReadLineAsync(timeout.Token);
            if (line != "READY")
            {
                Assert.Fail($"the program ended before it was ready:\n{await Within(stderr)}");
            }

            Assert.Equal(0, kill(child.Id, signal));
            // A program that listens for the signal itself decides when it ends: here it never does.
            var stays = listens ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(30);
            using var wait = new CancellationTokenSource(stays);
            var killed = false;
            try
            {
                await child.WaitForExitAsync(wait.Token);
            }
            catch (OperationCanceledException) when (listens)
            {
                killed = true;
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
            }

            // A process ended by a signal has the exit code 128 + the signal number.
            return new Ended(child.ExitCode - 128, killed, await Within(stderr), File.Exists(marker));
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
            }
        }
    }

    private static async Task<string> Within(Task<string> read) => await read.WaitAsync(TimeSpan.FromSeconds(10));

    private sealed record Ended(int Signal, bool Killed, string Stderr, bool Disposed);
}
