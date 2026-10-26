// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.ManagedProcess;
using ManagedProcessUnderTest = E2E.ManagedProcess;

namespace E2E.Tests.ManagedProcessIntegration;

/// <summary>
/// <c>tests/integration/managed-process.test.ts</c>. Not ported: the two <c>it.skipIf(win32)</c> tests about a surviving child after the command
/// leader exits on SIGTERM, since the port stops a process and its tree with <c>Process.Kill</c> and has no process group to signal.
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
