// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.ManagedProcess;
using ManagedProcessUnderTest = E2E.ManagedProcess;

namespace E2E.Tests.ManagedProcessIntegration;

/// <summary>
/// <c>tests/integration/managed-process.test.ts</c>. The two <c>it.skipIf(win32)</c> tests about a surviving child after the command leader
/// exits on SIGTERM return at once on Windows, as upstream skips them there (xUnit 2 cannot skip at run time).
/// </summary>
public sealed class ManagedProcessTests
{
    private static ManagedProcessUnderTest NodeApp(int port, string? extraArg = null, int startupTimeoutMs = 15_000, bool? reuseExisting = null) =>
        new(
            "app.command",
            AppProgram.Command(AppProgram.Serve(port, extraArg), TimeSpan.FromMilliseconds(startupTimeoutMs), TimeSpan.FromSeconds(2), reuseExisting: reuseExisting),
            Path.GetTempPath(),
            new Readiness("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/"));

    private static string UrlOf(int port) => "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/";

    [Fact]
    public async Task Starts_the_app_waits_for_readiness_and_stops_the_process_group_idempotently_and_safely_before_start()
    {
        var port = AppProgram.FreePort();
        var app = NodeApp(port);
        await app.StopAsync();
        await app.StartAsync();
        Assert.True(await AppProgram.IsReachable(UrlOf(port)));
        await app.StopAsync();
        await app.StopAsync();
        Assert.False(await AppProgram.IsReachable(UrlOf(port)));
    }

    [Fact]
    public async Task Escalates_to_SIGKILL_when_the_app_ignores_SIGTERM()
    {
        var port = AppProgram.FreePort();
        var app = NodeApp(port, "ignore-sigterm");
        await app.StartAsync();
        var stoppedAt = DateTime.UtcNow;
        await app.StopAsync();

        // shutdownTimeout (2s) + kill; anything much longer means the escalation hung.
        Assert.True(DateTime.UtcNow - stoppedAt < TimeSpan.FromSeconds(10));
        Assert.False(await AppProgram.IsReachable(UrlOf(port)));
    }

    [Fact]
    public async Task Stops_a_surviving_child_after_the_command_leader_exits_on_SIGTERM()
    {
        if (OperatingSystem.IsWindows())
        {
            // Upstream skips it there (it.skipIf(win32)): Windows has no process group to signal.
            return;
        }

        var port = AppProgram.FreePort();
        var dir = Directory.CreateTempSubdirectory("e2e-process-group-").FullName;
        var pidFile = Path.Combine(dir, "child.pid");
        var app = new ManagedProcessUnderTest(
            "app.command",
            AppProgram.Command(["wrap", pidFile, .. AppProgram.Serve(port, "ignore-sigterm")], shutdownTimeout: TimeSpan.FromMilliseconds(100)),
            dir,
            new Readiness(UrlOf(port)));
        try
        {
            await app.StartAsync();
            await app.StopAsync();
            await Unreachable(port);
            Assert.False(await AppProgram.IsReachable(UrlOf(port)));
            await app.StopAsync();
        }
        finally
        {
            await app.StopAsync();

            // Also release the owned descendant when the regression runs before the fix.
            KillQuietly(pidFile);
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Lets_a_child_finish_its_shutdown_after_the_leader_exits()
    {
        if (OperatingSystem.IsWindows())
        {
            // Upstream skips it there (it.skipIf(win32)): Windows has no process group to signal.
            return;
        }

        var port = AppProgram.FreePort();
        var dir = Directory.CreateTempSubdirectory("e2e-process-grace-").FullName;
        var pidFile = Path.Combine(dir, "child.pid");
        var finished = Path.Combine(dir, "finished");
        var app = new ManagedProcessUnderTest(
            "app.command",
            AppProgram.Command(["wrap", pidFile, "serve-graceful", port.ToString(System.Globalization.CultureInfo.InvariantCulture), finished], shutdownTimeout: TimeSpan.FromSeconds(1)),
            dir,
            new Readiness(UrlOf(port)));
        try
        {
            await app.StartAsync();
            await app.StopAsync();
            Assert.Equal("finished", await File.ReadAllTextAsync(finished));
            await Unreachable(port);
            Assert.False(await AppProgram.IsReachable(UrlOf(port)));
        }
        finally
        {
            await app.StopAsync();
            KillQuietly(pidFile);
            Directory.Delete(dir, true);
        }
    }

    // The child is signalled, not awaited, when the stop returns: waits (bounded) until its port no longer answers.
    private static async Task Unreachable(int port)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (await AppProgram.IsReachable(UrlOf(port)) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    // The child the leader started, killed when it is still there.
    private static void KillQuietly(string pidFile)
    {
        try
        {
            using var child = System.Diagnostics.Process.GetProcessById(int.Parse(File.ReadAllText(pidFile), System.Globalization.CultureInfo.InvariantCulture));
            child.Kill();
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or FormatException or System.ComponentModel.Win32Exception)
        {
            // already gone
        }
    }

    [Fact]
    public async Task Fails_with_APP_UNREACHABLE_when_the_command_exits_before_becoming_ready()
    {
        var app = new ManagedProcessUnderTest("app.command", AppProgram.Command(["exit", "3"]), Path.GetTempPath(), new Readiness(UrlOf(AppProgram.FreePort())));
        var failure = await Assert.ThrowsAsync<EngineException>(() => app.StartAsync());
        Assert.Equal("APP_UNREACHABLE", failure.Code);
        Assert.Contains("exited with code 3", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_with_APP_UNREACHABLE_when_the_executable_cannot_spawn()
    {
        var app = new ManagedProcessUnderTest(
            "app.command",
            new CommandConfig { Executable = Path.Combine(Path.GetTempPath(), "definitely", "not", "a", "real", "binary") },
            Path.GetTempPath(),
            new Readiness(UrlOf(AppProgram.FreePort())));
        var failure = await Assert.ThrowsAsync<EngineException>(() => app.StartAsync());
        Assert.Equal("APP_UNREACHABLE", failure.Code);
        Assert.Contains("failed to start", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// .NET-only (the process-group tests of upstream are not ported): the app a command started is ended with it even when the command
    /// has already exited. It holds on Windows, where the process runs in a job object; elsewhere only a process still running is read for its tree.
    /// </summary>
    [Fact]
    public async Task Ends_what_the_command_started_when_the_command_has_already_exited()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var port = AppProgram.FreePort();
        var app = new ManagedProcessUnderTest(
            "app.command",
            AppProgram.Command(["spawn", port.ToString(System.Globalization.CultureInfo.InvariantCulture)], TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(2)),
            Path.GetTempPath(),
            new Readiness(UrlOf(port)));
        try
        {
            await app.StartAsync();
        }
        catch (EngineException)
        {
            // The command exited before the app it started answered; the start fails and takes that app down with it.
        }

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (await AppProgram.IsReachable(UrlOf(port)) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.False(await AppProgram.IsReachable(UrlOf(port)));
        await app.StopAsync();
    }

    [Fact]
    public async Task Times_out_and_cleans_up_when_the_app_never_becomes_ready()
    {
        var app = new ManagedProcessUnderTest(
            "app.command",
            AppProgram.Command(["idle"], TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2)),
            Path.GetTempPath(),
            new Readiness(UrlOf(AppProgram.FreePort())));
        var failure = await Assert.ThrowsAsync<EngineException>(() => app.StartAsync());
        Assert.Contains("was not reachable", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_reuseExisting_starts_and_owns_the_app_when_nothing_answers_at_readyUrl_yet()
    {
        var port = AppProgram.FreePort();
        var app = NodeApp(port, reuseExisting: true);
        await app.StartAsync();
        Assert.True(app.Spawned);
        Assert.True(await AppProgram.IsReachable(UrlOf(port)));
        await app.StopAsync();
        Assert.False(await AppProgram.IsReachable(UrlOf(port)));
    }
}
