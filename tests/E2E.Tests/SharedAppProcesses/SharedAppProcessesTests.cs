// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Internal;
using E2E.Tests.ManagedProcess;
using SharedPool = E2E.SharedAppProcesses;

namespace E2E.Tests.SharedAppProcesses;

/// <summary>
/// <c>tests/integration/shared-app-processes.test.ts</c>: <c>SharedAppProcesses</c> under <c>StartDeclaredProcessesAsync</c>, the one flow a run
/// and a live session both start app processes through: attempts on fresh loads of one config share its app command, whatever else each opens, a
/// process that is stopping is started afresh only once it is gone, and a shared start outlives the attempt that began it. The port runs one
/// target per config, so a "load" here is the list of targets (a name and a resolved app) that upstream's <c>resolveConfig</c> would give; a
/// cancelled attempt surfaces as <see cref="OperationCanceledException"/> where upstream throws <c>CANCELLED</c>.
/// </summary>
public sealed class SharedAppProcessesTests : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-shared-apps-").FullName;
    private readonly int _port = AppProgram.FreePort();
    private readonly List<IAppProcesses> _started = [];
    private readonly string _url;
    private readonly CommandConfig _command;

    public SharedAppProcessesTests()
    {
        _url = "http://127.0.0.1:" + _port.ToString(CultureInfo.InvariantCulture);
        _command = Command();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // Everything a test started, stopped after it whatever the test asserted.
    public async Task DisposeAsync()
    {
        foreach (var processes in _started)
        {
            await processes.StopAsync(_ => { });
        }

        Directory.Delete(_dir, true);
    }

    [Fact]
    public async Task Shares_the_app_command_sessions_on_fresh_loads_both_declare_whatever_page_each_opens()
    {
        // A fresh load of one config: t0 opens the app's root, t1 a page of it, on one command.
        IReadOnlyList<(string Name, ResolvedApp App)> Config() => Load(new AppConfig { Url = _url, Command = _command }, new AppConfig { Url = _url + "/m", Command = _command });
        var pool = new SharedPool();
        var first = await Start(pool, Config());
        var second = await Start(pool, Config(), "t1");
        var third = await Start(pool, Config());
        Assert.Equal("app\n", StartupLog());
        await first.StopAsync(_ => { });
        Assert.True(await Serving());
        await third.StopAsync(_ => { });
        Assert.True(await Serving());
        await second.StopAsync(_ => { });
        Assert.False(await Serving());
    }

    [Fact]
    public async Task Waits_for_a_process_that_is_stopping_before_it_starts_it_again()
    {
        var config = Load(new AppConfig { Url = _url, Command = _command });
        var pool = new SharedPool();
        var first = await Start(pool, config);
        var stopping = first.StopAsync(_ => { });
        var second = await Start(pool, config);
        await stopping;
        Assert.Equal("app\napp\n", StartupLog());
        Assert.True(await Serving());
        await second.StopAsync(_ => { });
        Assert.False(await Serving());
    }

    [Fact]
    public async Task Keeps_starting_a_shared_process_when_the_attempt_that_began_it_is_aborted_while_another_waits()
    {
        var config = Load(new AppConfig { Url = _url, Command = _command });
        var pool = new SharedPool();
        using var aborted = new CancellationTokenSource();
        var first = Start(pool, config, "t0", aborted.Token);
        var second = Start(pool, config);
        await aborted.CancelAsync();

        // The attempt that gave up stops waiting at once; the start goes on for the other.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await second;
        Assert.True(await Serving());
        await (await second).StopAsync(_ => { });
        Assert.False(await Serving());
    }

    [Fact]
    public async Task Aborts_a_hung_start_once_every_attempt_waiting_on_it_gave_up_stopping_the_process_it_started()
    {
        // The server answers, but the readiness probe never does: the start hangs with the process up.
        var config = Load(new AppConfig { Url = _url, ReadyUrl = "http://127.0.0.1:1/", Command = Command(TimeSpan.FromMilliseconds(600_000)) });
        var pool = new SharedPool();
        using var cancel = new CancellationTokenSource();
        var opening = Start(pool, config, "t0", cancel.Token);
        await Poll(true);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);

        // Nobody holds the abandoned start, so the pool stops what it spawned.
        await Poll(false);

        // The next attempt starts it afresh rather than waiting on the abandoned start.
        using var retry = new CancellationTokenSource();
        var again = Start(pool, config, "t0", retry.Token);
        await Poll(true);
        await retry.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => again);
        await Poll(false);
        Assert.Equal("app\napp\n", StartupLog());
    }

    private CommandConfig Command(TimeSpan? startupTimeout = null) => new()
    {
        Executable = AppProgram.Executable,
        Args = ["serve-logged", _port.ToString(CultureInfo.InvariantCulture)],
        StartupTimeout = startupTimeout,
        InheritEnv = ["DOTNET_ROOT"],
    };

    // One config load, as a live session loads it: one target per declaration, named t0, t1, and so on.
    private IReadOnlyList<(string Name, ResolvedApp App)> Load(params AppConfig[] declared) =>
        [.. declared.Select((app, index) => ("t" + index.ToString(CultureInfo.InvariantCulture), TargetApps.ResolveTargetApp("t" + index.ToString(CultureInfo.InvariantCulture), app, _dir)))];

    private async Task<IAppProcesses> Start(SharedPool pool, IReadOnlyList<(string Name, ResolvedApp App)> config, string name = "t0", CancellationToken cancellationToken = default)
    {
        var processes = await Provision.StartDeclaredProcessesAsync(
            config.Where(target => target.Name == name),
            _dir,
            () => new ManagedProcessHooks { Ci = false },
            new DebugTrace(false),
            cancellationToken,
            pool);
        _started.Add(processes);
        return processes;
    }

    private string StartupLog() => File.ReadAllText(Path.Combine(_dir, "startup.log"));

    private Task<bool> Serving() => AppProgram.IsReachable(_url);

    // Polls until the server answers (or stops answering), for at most 20 seconds.
    private async Task Poll(bool expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (await Serving() != expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.Equal(expected, await Serving());
    }
}
