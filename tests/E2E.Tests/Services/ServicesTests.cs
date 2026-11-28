// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.Json;
using E2E.Tests.ManagedProcess;

namespace E2E.Tests.Services;

/// <summary>
/// Upstream's <c>describe('services')</c> in <c>services.test.ts</c>. The port runs a test in the process that started the services, so the
/// worker-process, copy, and wire-protocol <c>it</c>s have nothing to port (named in the PR report), and the runner's report and exit code
/// are read as the exception the start throws.
/// </summary>
public sealed class ServicesTests
{
    private static readonly IReadOnlyDictionary<string, string?> NoEnv = new Dictionary<string, string?>();

    [Fact]
    public async Task Partial_Calls_an_env_function_once_after_the_services_start_with_port_expanded()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-services-").FullName;
        var log = Path.Combine(dir, "events.log");
        var seen = new List<ServiceValues>();
        var calls = 0;
        var config = CommandConfig(
                dir,
                ["serve-env", "{port}", log, "ALPHA_URL"],
                new Dictionary<string, Service?> { ["alpha"] = Yielding(new Alpha("http://alpha.test")) }
            )
            .WithCommandEnv("fake", (services, _) =>
            {
                Interlocked.Increment(ref calls);
                seen.Add(services);
                return Task.FromResult<IReadOnlyDictionary<string, string?>>(
                    new Dictionary<string, string?>
                    {
                        ["PORT"] = "{port}",
                        ["ALPHA_URL"] = services.Get<Alpha>("alpha").Url,
                    }
                );
            });
        var notices = new List<string>();
        var (target, hold) = await E2E.RunApps.AcquireAsync(config, config.Target, notices.Add, CancellationToken.None);
        try
        {
            Assert.True(await AppProgram.IsReachable(target.App.Url!));
            Assert.Equal(1, calls);
            Assert.Equal("http://alpha.test", seen.Single().Get<Alpha>("alpha").Url);
            Assert.Equal(["ALPHA_URL=http://alpha.test"], File.ReadAllLines(log));

            // The runner started the app itself, so it got the variables and there is nothing to warn about.
            Assert.DoesNotContain(notices, notice =>
                notice.Contains("didn't get the variables", StringComparison.Ordinal)
            );
        }
        finally
        {
            await hold.StopAsync(_ => { });
        }
    }

    [Fact]
    public async Task Keeps_the_code_of_an_error_an_env_function_throws_with_one()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-services-").FullName;
        var config = CommandConfig(dir, ["serve", "{port}"], new Dictionary<string, Service?>())
            .WithCommandEnv("fake", (_, _) => throw new EngineException("APP_UNREACHABLE", "the database is down"));
        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            E2E.RunApps.AcquireAsync(config, config.Target, null, CancellationToken.None)
        );
        Assert.Equal("APP_UNREACHABLE", failure.Code);
        Assert.Equal("the database is down", failure.Message);
    }

    [Fact]
    public async Task Drops_an_undefined_value_an_env_function_returns_so_the_app_keeps_the_variable_it_inherits()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-services-").FullName;
        var log = Path.Combine(dir, "events.log");
        var config = CommandConfig(
                dir,
                ["serve-env", "{port}", log, "E2E_SERVICES_TEST_ALPHA"],
                new Dictionary<string, Service?>(),
                ["E2E_SERVICES_TEST_ALPHA"]
            )
            .WithCommandEnv("fake", (_, _) =>
                Task.FromResult<IReadOnlyDictionary<string, string?>>(
                    new Dictionary<string, string?> { ["E2E_SERVICES_TEST_ALPHA"] = null }
                )
            );
        Environment.SetEnvironmentVariable("E2E_SERVICES_TEST_ALPHA", "inherited");
        try
        {
            var (target, hold) = await E2E.RunApps.AcquireAsync(config, config.Target, null, CancellationToken.None);
            try
            {
                Assert.True(await AppProgram.IsReachable(target.App.Url!));
                Assert.Equal(["E2E_SERVICES_TEST_ALPHA=inherited"], File.ReadAllLines(log));
            }
            finally
            {
                await hold.StopAsync(_ => { });
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("E2E_SERVICES_TEST_ALPHA", null);
        }
    }

    [Fact]
    public async Task Partial_Fails_with_APP_UNREACHABLE_when_an_env_function_does_not_return_within_startupTimeout_and_stops_the_services()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-services-").FullName;
        var events = new List<string>();
        var config = CommandConfig(
                dir,
                ["serve", "{port}"],
                new Dictionary<string, Service?>
                {
                    ["alpha"] = Define.DefineService(context => Recording(events, "alpha", context)),
                },
                startupTimeout: 200
            )
            .WithCommandEnv("fake", (_, token) =>
                Task.Delay(Timeout.Infinite, token).ContinueWith(_ => NoEnv, TaskScheduler.Default)
            );
        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            E2E.RunApps.AcquireAsync(config, config.Target, null, CancellationToken.None)
        );
        Assert.Equal("APP_UNREACHABLE", failure.Code);
        Assert.Equal("target \"fake\" app.command.env didn't return within 200 ms", failure.Message);
        Assert.Equal(["start alpha", "stop alpha"], events);
    }

    [Fact]
    public async Task Partial_Stops_the_services_that_started_when_a_later_one_fails_to_start_and_starts_no_app_command()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-services-").FullName;
        var log = Path.Combine(dir, "events.log");
        var events = new List<string>();
        var config = CommandConfig(
            dir,
            ["serve-env", "{port}", log, "PORT"],
            new Dictionary<string, Service?>
            {
                ["first"] = Define.DefineService(context => Recording(events, "first", context)),
                ["broken"] = Define.DefineService(context => Failing(events, "broken", "port 4010 is taken")),
                ["never"] = Define.DefineService(context => Recording(events, "never", context)),
            }
        );
        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            E2E.RunApps.AcquireAsync(config, config.Target, null, CancellationToken.None)
        );
        Assert.Equal(["start first", "start broken", "stop first"], events);
        Assert.Equal("SERVICE_START_FAILED", failure.Code);
        Assert.Equal("service \"broken\" failed to start: port 4010 is taken", failure.Message);
        Assert.False(File.Exists(log));
    }

    [Fact]
    public async Task Partial_Reports_a_cleanup_error_for_a_service_that_fails_to_stop_after_a_later_one_fails_to_start()
    {
        var stopFailures = new List<Exception>();
        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            RunServices.StartAsync(
                [
                    ("first", Define.DefineService(_ => StopThrows("could not flush the inbox"))),
                    ("broken", Define.DefineService(_ => Failing(new List<string>(), "broken", "port 4010 is taken"))),
                ],
                CancellationToken.None,
                CancellationToken.None,
                new ServiceHooks { StopFailed = stopFailures.Add }
            )
        );
        Assert.Equal("service \"broken\" failed to start: port 4010 is taken", failure.Message);
        var cleanup = Assert.IsType<EngineException>(Assert.Single(stopFailures));
        Assert.Equal("SERVICE_STOP_FAILED", cleanup.Code);
        Assert.Equal("service \"first\" failed to stop: could not flush the inbox", cleanup.Message);
    }

    [Fact]
    public async Task Partial_Reports_service_inbox_finished_without_yielding_Yield_a_value_once_between_the_startup_code_and_the_stop_code()
    {
        var failure = await StartFails(Define.DefineService<object>(_ => Empty()));
        Assert.Equal("SERVICE_START_FAILED", failure.Code);
        Assert.Equal(
            "service \"inbox\" finished without yielding. Yield a value once between the startup code and the stop code",
            failure.Message
        );
    }

    [Fact]
    public async Task Partial_Reports_service_inbox_failed_to_start_no_inbox_today()
    {
        var failure = await StartFails(
            Define.DefineService<object>(_ => Failing([], "inbox", "no inbox today", false))
        );
        Assert.Equal("SERVICE_START_FAILED", failure.Code);
        Assert.Equal("service \"inbox\" failed to start: no inbox today", failure.Message);
    }

    [Fact]
    public async Task Partial_Reports_service_inbox_failed_to_stop_could_not_flush_the_inbox()
    {
        var failures = new List<Exception>();
        var services = await RunServices.StartAsync(
            [("inbox", Define.DefineService(_ => StopThrows("could not flush the inbox")))],
            CancellationToken.None,
            CancellationToken.None,
            new ServiceHooks()
        );
        await services.StopAsync(failures.Add);
        var failure = Assert.IsType<EngineException>(Assert.Single(failures));
        Assert.Equal("SERVICE_STOP_FAILED", failure.Code);
        Assert.Equal("service \"inbox\" failed to stop: could not flush the inbox", failure.Message);
    }

    [Fact]
    public async Task Partial_Aborts_the_signal_and_fails_the_run_when_a_service_does_not_yield_within_the_start_limit()
    {
        var reason = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = Define.DefineService<object>(
            context => WaitsForAbort(context.Signal, () => reason.TrySetResult(true)),
            startupTimeout: TimeSpan.FromMilliseconds(100)
        );
        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            RunServices.StartAsync([("slow", slow)], CancellationToken.None, CancellationToken.None, new ServiceHooks())
        );
        Assert.Equal("SERVICE_START_FAILED", failure.Code);
        Assert.Equal("service \"slow\" didn't yield within 0.1 seconds", failure.Message);
        Assert.True(await reason.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Partial_Starts_no_service_code_when_a_handler_of_the_starting_event_cancels_the_run()
    {
        using var interrupt = new CancellationTokenSource();
        var entered = false;
        var inbox = Define.DefineService(_ => Entered(() => entered = true));
        var started = await RunServices.StartAsync(
            [("inbox", inbox)],
            interrupt.Token,
            CancellationToken.None,
            new ServiceHooks { Starting = _ => interrupt.Cancel() }
        );
        Assert.False(entered);
        Assert.Empty(started.Values);
    }

    [Fact]
    public async Task Partial_Aborts_the_signal_and_reports_a_cleanup_error_when_the_stop_code_does_not_finish_within_the_stop_limit()
    {
        var aborted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stuck = Define.DefineService(
            context => StopHangs(context.Signal, () => aborted.TrySetResult(true), []),
            shutdownTimeout: TimeSpan.FromMilliseconds(100)
        );
        var services = await RunServices.StartAsync(
            [("stuck", stuck)],
            CancellationToken.None,
            CancellationToken.None,
            new ServiceHooks()
        );
        var failures = new List<Exception>();
        await services.StopAsync(failures.Add);
        var failure = Assert.IsType<EngineException>(Assert.Single(failures));
        Assert.Equal("SERVICE_STOP_FAILED", failure.Code);
        Assert.Equal("service \"stuck\" didn't stop within 0.1 seconds", failure.Message);
        Assert.True(await aborted.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Partial_Ends_a_service_that_yields_a_second_time_and_runs_its_finally_block()
    {
        var finished = false;
        var twice = Define.DefineService(_ => YieldsTwice(() => finished = true));
        var services = await RunServices.StartAsync(
            [("twice", twice)],
            CancellationToken.None,
            CancellationToken.None,
            new ServiceHooks()
        );
        var failures = new List<Exception>();
        await services.StopAsync(failures.Add);
        var failure = Assert.IsType<EngineException>(Assert.Single(failures));
        Assert.Equal("SERVICE_STOP_FAILED", failure.Code);
        Assert.Equal(
            "service \"twice\" yielded a second time. Yield only once, and put stop code after the yield",
            failure.Message
        );
        Assert.True(finished);
    }

    [Fact]
    public async Task Partial_Stops_the_services_before_one_that_fails_to_stop_whichever_way_it_fails()
    {
        var events = new List<string>();
        var stuckAborted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = await RunServices.StartAsync(
            [
                ("first", Define.DefineService(context => Recording(events, "first", context))),
                ("throwing", Define.DefineService(_ => StopThrows("could not flush the inbox", events, "throwing"))),
                ("twice", Define.DefineService(_ => YieldsTwiceLogged(events))),
                (
                    "stuck",
                    Define.DefineService(
                        context => StopHangs(context.Signal, () => stuckAborted.TrySetResult(true), events),
                        shutdownTimeout: TimeSpan.FromMilliseconds(100)
                    )
                ),
            ],
            CancellationToken.None,
            CancellationToken.None,
            new ServiceHooks()
        );
        var failures = new List<Exception>();
        await services.StopAsync(failures.Add);
        Assert.Equal(
            [
                "SERVICE_STOP_FAILED service \"stuck\" didn't stop within 0.1 seconds",
                "SERVICE_STOP_FAILED service \"twice\" yielded a second time. Yield only once, and put stop code after the yield",
                "SERVICE_STOP_FAILED service \"throwing\" failed to stop: could not flush the inbox",
            ],
            failures.Cast<EngineException>().Select(failure => failure.Code + " " + failure.Message)
        );
        Assert.Equal(["start first", "stop stuck", "stop twice", "stop throwing", "stop first"], events);
        Assert.True(await stuckAborted.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void Keeps_the_config_digest_and_with_it_the_replay_cache_when_services_or_an_env_function_are_added()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-services-").FullName;
        var plain = CommandConfig(dir, ["serve", "{port}"], new Dictionary<string, Service?>());
        var service = Define.DefineService(_ => Entered(() => { }));
        var withService = plain.WithService("service", service);
        var withEnv = plain.WithCommandEnv("fake", (_, _) => Task.FromResult(NoEnv));
        Assert.Equal(ResolvedConfig.ComputeConfigDigest(plain), ResolvedConfig.ComputeConfigDigest(withService));
        Assert.Equal(ResolvedConfig.ComputeConfigDigest(plain), ResolvedConfig.ComputeConfigDigest(withEnv));
    }

    private static async Task<EngineException> StartFails(Service service) =>
        await Assert.ThrowsAsync<EngineException>(() =>
            RunServices.StartAsync(
                [("inbox", service)],
                CancellationToken.None,
                CancellationToken.None,
                new ServiceHooks()
            )
        );

    private static E2EConfig CommandConfig(
        string dir,
        string[] args,
        IReadOnlyDictionary<string, Service?> services,
        string[]? inheritEnv = null,
        int startupTimeout = 20000
    )
    {
        var config = E2EConfig.Parse(
            $$"""
            { "targets": [{ "name": "fake", "app": { "url": "http://127.0.0.1:0",
              "command": { "executable": {{JsonSerializer.Serialize(
                AppProgram.Executable
            )}}, "args": {{JsonSerializer.Serialize(args)}},
                "startupTimeout": {{startupTimeout}}, "shutdownTimeout": 2000,
                "inheritEnv": {{JsonSerializer.Serialize(new[] { "DOTNET_ROOT" }.Concat(inheritEnv ?? []))}} } } }] }
            """,
            dir,
            _ => null
        );
        foreach (var (name, service) in services)
        {
            config = config.WithService(name, service);
        }

        return config;
    }

    private static Service Yielding(object value) => Define.DefineService(_ => Once(value));

    private static async IAsyncEnumerable<object> Once(object value)
    {
        await Task.Yield();
        yield return value;
    }

    private static async IAsyncEnumerable<object> Empty()
    {
        await Task.Yield();
        yield break;
    }

    private static async IAsyncEnumerable<object> Entered(Action entered)
    {
        entered();
        await Task.Yield();
        yield return new object();
    }

    private static async IAsyncEnumerable<object> Recording(
        List<string> events,
        string name,
        ServiceStartContext context
    )
    {
        _ = context;
        events.Add("start " + name);
        try
        {
            await Task.Yield();
            yield return new object();
        }
        finally
        {
            events.Add("stop " + name);
        }
    }

    private static async IAsyncEnumerable<object> Failing(
        List<string> events,
        string name,
        string message,
        bool log = true
    )
    {
        if (log)
        {
            events.Add("start " + name);
        }

        await Task.Yield();
        throw new InvalidOperationException(message);
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<object> StopThrows(
        string message,
        List<string>? events = null,
        string name = ""
    )
    {
        await Task.Yield();
        yield return new object();
        events?.Add("stop " + name);
        throw new InvalidOperationException(message);
    }

    private static async IAsyncEnumerable<object> WaitsForAbort(
        [EnumeratorCancellation] CancellationToken signal,
        Action aborted
    )
    {
        try
        {
            await Task.Delay(Timeout.Infinite, signal).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            aborted();
        }

        yield break;
    }

    private static async IAsyncEnumerable<object> StopHangs(
        [EnumeratorCancellation] CancellationToken signal,
        Action aborted,
        List<string> events
    )
    {
        await Task.Yield();
        yield return new object();
        events.Add("stop stuck");
        try
        {
            await Task.Delay(Timeout.Infinite, signal).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            aborted();
        }
    }

    private static async IAsyncEnumerable<object> YieldsTwice(Action finished)
    {
        try
        {
            await Task.Yield();
            yield return new object();
            yield return new object();
        }
        finally
        {
            finished();
        }
    }

    private static async IAsyncEnumerable<object> YieldsTwiceLogged(List<string> events)
    {
        await Task.Yield();
        yield return new object();
        events.Add("stop twice");
        yield return new object();
    }

    private sealed record Alpha(string Url);
}
