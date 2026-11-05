// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
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

    // Actual processes, with each stop shared by the pool and teardown.
    private readonly ConcurrentQueue<IAppProcesses> _started = [];

    // Calls waiting on the pool, which may reject before their processes stop.
    private readonly ConcurrentQueue<Task> _starting = [];

    // Actual starts, including the cleanup they await when aborted.
    private readonly ConcurrentQueue<Task> _launching = [];

    // Aborts the starts a failed test left waiting.
    private readonly CancellationTokenSource _teardown = new();
    private readonly ConcurrentQueue<CancellationTokenSource> _scopes = [];
    private readonly string _url;
    private readonly CommandConfig _command;

    public SharedAppProcessesTests()
    {
        _url = "http://127.0.0.1:" + _port.ToString(CultureInfo.InvariantCulture);
        _command = Command();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await StopProcesses();
        Directory.Delete(_dir, true);
    }

    // Stops both acquired processes and starts abandoned by their callers, whatever the test asserted.
    private async Task StopProcesses()
    {
        await _teardown.CancelAsync();
        await Task.WhenAll(_starting.Select(Settled));
        await Task.WhenAll(_launching.Select(Settled));
        try
        {
            foreach (var processes in _started)
            {
                await processes.StopAsync(_ => { });
            }
        }
        finally
        {
            foreach (var scope in _scopes)
            {
                scope.Dispose();
            }
        }
    }

    private static async Task Settled(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception)
        {
            // Like allSettled: a failure is the test's to assert, not teardown's.
        }
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
        await Task.WhenAll(_launching.Select(Settled));
        await Poll(false);

        // The next attempt starts it afresh rather than waiting on the abandoned start.
        using var retry = new CancellationTokenSource();
        var again = Start(pool, config, "t0", retry.Token);
        await Poll(true);
        await retry.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => again);
        await Task.WhenAll(_launching.Select(Settled));
        await Poll(false);
        Assert.Equal("app\napp\n", StartupLog());
    }

    [Fact]
    public async Task Waits_for_a_stop_already_running_in_the_pool_when_teardown_begins()
    {
        if (OperatingSystem.IsWindows())
        {
            // Upstream skips it there (it.skipIf(win32)): Windows has no SIGTERM handler to hold the shutdown.
            return;
        }

        var stoppingFile = Path.Combine(_dir, "stopping");
        var releaseFile = Path.Combine(_dir, "release-stop");
        var config = Load(new AppConfig { Url = _url, Command = ShutdownCommand("serve-hold") });
        var first = await Start(new SharedPool(), config);
        var stopping = first.StopAsync(_ => { });
        Task? cleaning = null;
        try
        {
            await Poll(() => File.Exists(stoppingFile), true);
            var cleaned = false;
            cleaning = StopProcesses().ContinueWith(_ => cleaned = true, TaskScheduler.Default);

            // An HTTP round trip also lets a prematurely completed teardown finish its continuations.
            Assert.True(await Serving());
            Assert.False(cleaned);
        }
        finally
        {
            await File.WriteAllTextAsync(releaseFile, "released");
            await stopping;
            if (cleaning is not null)
            {
                await cleaning;
            }
        }

        Assert.False(await Serving());
    }

    [Fact]
    public async Task Keeps_the_project_until_shutdown_completes_in_the_actual_afterEach_hook()
    {
        if (OperatingSystem.IsWindows())
        {
            // Upstream skips it there (it.skipIf(win32)): Windows has no SIGTERM handler to delay the shutdown.
            return;
        }

        // The actual hook is DisposeAsync of a test instance: it runs here as the runner would run it, and the checks follow it.
        var evidence = Directory.CreateTempSubdirectory("e2e-shared-apps-shutdown-").FullName;
        var receipt = Path.Combine(evidence, "shutdown.json");
        var test = new SharedAppProcessesTests();
        try
        {
            var config = test.Load(new AppConfig
            {
                Url = test._url,
                ReadyUrl = "http://127.0.0.1:1/",
                Command = test.ShutdownCommand("serve-receipt", receipt, test._dir),
            });
            _ = test.Start(new SharedPool(), config);
            await test.Poll(true);
            await test.DisposeAsync();
            Assert.False(await test.Serving());
            Assert.False(Directory.Exists(test._dir));
            Assert.Equal("{\"projectExists\":true}", await File.ReadAllTextAsync(receipt));
        }
        finally
        {
            await test.StopProcesses();
            if (Directory.Exists(test._dir))
            {
                Directory.Delete(test._dir, true);
            }

            Directory.Delete(evidence, true);
        }
    }

    private CommandConfig Command(TimeSpan? startupTimeout = null) => new()
    {
        Executable = AppProgram.Executable,
        Args = ["serve-logged", _port.ToString(CultureInfo.InvariantCulture)],
        StartupTimeout = startupTimeout,
        InheritEnv = ["DOTNET_ROOT"],
    };

    // A server whose SIGTERM handler holds the shutdown (serve-hold) or records its project (serve-receipt).
    private CommandConfig ShutdownCommand(params string[] mode) => new()
    {
        Executable = AppProgram.Executable,
        Args = [mode[0], _port.ToString(CultureInfo.InvariantCulture), .. mode.Skip(1)],
        InheritEnv = ["DOTNET_ROOT"],
    };

    // One config load, as a live session loads it: one target per declaration, named t0, t1, and so on.
    private IReadOnlyList<(string Name, ResolvedApp App)> Load(params AppConfig[] declared) =>
        [.. declared.Select((app, index) => ("t" + index.ToString(CultureInfo.InvariantCulture), TargetApps.ResolveTargetApp("t" + index.ToString(CultureInfo.InvariantCulture), app, _dir)))];

    private async Task<IAppProcesses> Start(SharedPool pool, IReadOnlyList<(string Name, ResolvedApp App)> config, string name = "t0", CancellationToken cancellationToken = default)
    {
        var scope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _teardown.Token);
        _scopes.Enqueue(scope);
        var opening = Provision.StartDeclaredProcessesAsync(
            config.Where(target => target.Name == name),
            _dir,
            () => new ManagedProcessHooks { Ci = false },
            new DebugTrace(false),
            scope.Token,
            new TrackingPool(pool, this));
        _starting.Enqueue(opening);
        return await opening;
    }

    // Tracks the pool's actual starts and shares each stop with teardown.
    private sealed class TrackingPool(SharedPool pool, SharedAppProcessesTests test) : IProcessPool
    {
        public Task<IAppProcesses> AcquireAsync(string key, Func<CancellationToken, Task<IAppProcesses>> start, CancellationToken cancellationToken) =>
            pool.AcquireAsync(
                key,
                async scope =>
                {
                    var pending = start(scope);
                    test._launching.Enqueue(pending);
                    var tracked = new SharedStop(await pending);
                    test._started.Enqueue(tracked);
                    return tracked;
                },
                cancellationToken);
    }

    private sealed class SharedStop(IAppProcesses processes) : IAppProcesses
    {
        private readonly Lock _gate = new();
        private Task? _stopping;

        public bool Running => processes.Running;

        public Task StopAsync(Action<Exception> onFailure)
        {
            lock (_gate)
            {
                return _stopping ??= processes.StopAsync(onFailure);
            }
        }
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

    // Polls until the condition holds, for at most 20 seconds.
    private static async Task Poll(Func<bool> condition, bool expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (condition() != expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.Equal(expected, condition());
    }
}
