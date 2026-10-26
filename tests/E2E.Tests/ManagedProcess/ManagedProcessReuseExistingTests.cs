// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using ManagedProcessUnderTest = E2E.ManagedProcess;

namespace E2E.Tests.ManagedProcess;

public sealed class ManagedProcessReuseExistingTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-reuse-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>A command that stays up like a server would.</summary>
    private static CommandConfig ServerApp(bool reuseExisting, TimeSpan? startupTimeout = null) =>
        AppProgram.Command(["idle"], startupTimeout ?? TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2), reuseExisting: reuseExisting);

    [Fact]
    public async Task Attaches_to_an_app_already_answering_at_readyUrl_nothing_spawns_and_stop_leaves_it_running()
    {
        await using var running = TestServer.Answering();
        var notices = new List<string>();
        var app = new ManagedProcessUnderTest("app.command", ServerApp(true), _dir, new Readiness(running.Url), new ManagedProcessHooks { Ci = false, Notice = notices.Add });
        await app.StartAsync();
        Assert.False(app.Spawned);
        Assert.Equal([$"app.command: reusing the process already serving {running.Url}"], notices);
        await app.StopAsync();
        Assert.True(await AppProgram.IsReachable(running.Url));
    }

    [Fact]
    public async Task Ignores_reuseExisting_in_CI_an_already_answering_URL_is_APP_ALREADY_RUNNING_nothing_spawns()
    {
        await using var running = TestServer.Answering();
        var notices = new List<string>();
        var app = new ManagedProcessUnderTest("app.command", ServerApp(true), _dir, new Readiness(running.Url), new ManagedProcessHooks { Ci = true, Notice = notices.Add });
        var failure = await Assert.ThrowsAsync<EngineException>(() => app.StartAsync());
        Assert.Equal("APP_ALREADY_RUNNING", failure.Code);
        Assert.Equal($"{running.Url} already answered before app.command started; stop that process (reuseExisting is ignored in CI)", failure.Message);
        Assert.False(app.Spawned);
        Assert.Equal(["app.command: reuseExisting is ignored in CI, starting the command"], notices);
        await app.StopAsync();
        Assert.True(await AppProgram.IsReachable(running.Url));
    }

    [Fact]
    public async Task Without_reuseExisting_an_already_answering_URL_is_APP_ALREADY_RUNNING_instead_of_passing_as_the_new_command()
    {
        await using var running = TestServer.Answering();
        var notices = new List<string>();
        var app = new ManagedProcessUnderTest("app.command", ServerApp(false), _dir, new Readiness(running.Url), new ManagedProcessHooks { Ci = false, Notice = notices.Add });
        var failure = await Assert.ThrowsAsync<EngineException>(() => app.StartAsync());
        Assert.Equal("APP_ALREADY_RUNNING", failure.Code);
        Assert.Equal($"{running.Url} already answered before app.command started; stop that process or set reuseExisting: true", failure.Message);
        Assert.False(app.Spawned);
        Assert.Empty(notices);
        await app.StopAsync();
        Assert.True(await AppProgram.IsReachable(running.Url));
    }

    [Fact]
    public async Task Spends_the_preflight_probe_from_startupTimeout_a_slow_answer_past_the_budget_is_APP_UNREACHABLE_on_time()
    {
        // A server that takes longer than the whole budget to answer: the probe must give up within it.
        await using var server = new TestServer(async _ =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            return 200;
        });
        var app = new ManagedProcessUnderTest("app.command", ServerApp(true, TimeSpan.FromMilliseconds(300)), _dir, new Readiness(server.Url), new ManagedProcessHooks { Ci = false });
        var startedAt = DateTime.UtcNow;
        var failure = await Assert.ThrowsAsync<EngineException>(() => app.StartAsync());
        var elapsed = DateTime.UtcNow - startedAt;
        Assert.Equal("APP_UNREACHABLE", failure.Code);
        Assert.Contains("within 300 ms", failure.Message, StringComparison.Ordinal);

        // Well under the 2 s probe cap plus the 300 ms budget the old code would have spent back to back.
        Assert.True(elapsed < TimeSpan.FromMilliseconds(1_500), "took " + elapsed);
        await app.StopAsync();
    }
}
