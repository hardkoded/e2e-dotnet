// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.OpenSession;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Tests.OpenHostDeadline;

/// <summary>
/// Upstream's <c>describe('openHost: deadline')</c> (<c>tests/integration/open-host-deadline.test.ts</c>). The <c>it</c>s that need no feature the port
/// lacks are ported here. Not ported (hardkoded/e2e-dotnet#2002, #2041, #2099): <c>records abandoned polls and steps without unhandled rejections or reads after close</c> (hardkoded/e2e-dotnet#2138: the port has no
/// <c>PollScope</c> in a hosted session, which ends a poll its phase did not await). The signals of <c>cancels an in-flight poll on %s</c> are the open tokens of <c>OpenAsync</c> and <c>OpenSessionAsync</c> (C# has no <c>signal</c> option).
/// The <c>Partial_</c> <c>it</c>s: the two contributed-fixture <c>it</c>s (<c>caps contributed fixture calls...</c>, <c>keeps the session deadline when a contributed operation disables its own timeout</c>) reach the fixture through the fake engine's <c>Gadget</c> instead of <c>session.fixtures.gadget</c>, since a hosted session's <c>Fixtures</c> is the <c>TestContext</c> and the port's operations declare no timeout of their own (so <c>timeout: false</c> has nothing to disable); <c>records a cancelled fixture as interrupted</c> cancels a core operation, as the port has no <c>extend</c> for a <c>gadget</c> fixture; <c>refuses a session timeout</c> lacks the NaN and infinite cases (a <see cref="TimeSpan"/> has neither), <c>times out a session closed past its deadline</c> uses a 60 s timeout, not upstream's 20 ms (see its comment), and the <c>setTimeout</c> one: upstream calls the global <c>test.setTimeout</c>, which finds the session through <c>globalThis</c>, where the port's
/// <see cref="E2E.Internal.TestTimeout.SetRunningTestTimeout"/> takes the session.
/// </summary>
public sealed class OpenHostDeadlineTests : IAsyncLifetime
{
    private const string Config =
        """{ "trace": "on", "targets": [{ "name": "kiosk", "platform": "web", "app": { "url": "http://127.0.0.1:4173/" } }] }""";

    private static readonly IReadOnlyDictionary<string, string?> NoEnv = new Dictionary<string, string?>();

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-open-host-deadline-").FullName;
    private readonly List<E2EHost> _hosts = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            foreach (var host in _hosts)
            {
                await host.CloseAsync();
            }
        }
        finally
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private async Task<E2EHost> OpenAsync(
        HostFakeEngine engine,
        string? config = null,
        CancellationToken cancellationToken = default
    )
    {
        File.WriteAllText(Path.Combine(_dir, E2EConfig.FileName), config ?? Config);
        var host = await E2EHost.OpenAsync(
            new OpenHostOptions
            {
                Cwd = _dir,
                Env = NoEnv,
                EngineFactory = _ => engine,
            },
            cancellationToken
        );
        _hosts.Add(host);
        return host;
    }

    private static string ConfigWith(string keys) =>
        """{ "trace": "on", """
        + keys
        + """, "targets": [{ "name": "kiosk", "platform": "web", "app": { "url": "http://127.0.0.1:4173/" } }] }""";

    private static PollOptions Every(int ms, TimeSpan? timeout = null) =>
        new() { Intervals = [TimeSpan.FromMilliseconds(ms)], Timeout = timeout };

    [Fact]
    public async Task Bounds_polls_by_config_assertionTimeout_and_refreshes_the_deadline_for_the_next_session()
    {
        var engine = new HostFakeEngine();
        var host = await OpenAsync(engine, ConfigWith("\"assertionTimeout\": 40"));
        var first = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "short poll", Timeout = TimeSpan.FromMilliseconds(200) }
        );
        var error = await Assert.ThrowsAsync<TestException>(() => Expect.Poll(() => false, Every(5)).ToBeAsync(true));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Contains("timed out after 40 ms", error.Message);
        Assert.Empty(await first.CloseAsync());

        var second = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "fresh deadline", Timeout = TimeSpan.FromSeconds(5) }
        );
        var reads = 0;
        await Expect.Poll(() => ++reads, Every(5, TimeSpan.FromMilliseconds(500))).ToSatisfyAsync(value => value > 2);
        await second.App.OpenAsync();
        var operation = engine.Operations.FirstOrDefault(entry => entry.AttemptId == second.AttemptId);
        Assert.True(operation?.Timeout > TimeSpan.FromMilliseconds(200));
        Assert.Equal(host.RunId, operation?.RunId);
        Assert.Empty(await second.CloseAsync());
    }

    [Fact]
    public async Task Partial_Applies_the_project_toPass_timeout_in_a_hosted_session()
    {
        var host = await OpenAsync(new HostFakeEngine(), ConfigWith("\"toPass\": { \"timeout\": 40 }"));
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "configured toPass", Timeout = TimeSpan.FromSeconds(5) }
        );
        var error = await Assert.ThrowsAsync<TestException>(() => Expect.That(() => { }).Not.ToPassAsync());
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Equal("expect(...).not.toPass() timed out after 40 ms\nlast: the block passed", error.Message);
        Assert.Empty(await session.CloseAsync());
    }

    [Fact]
    public async Task Caps_an_explicitly_long_poll_at_the_session_deadline()
    {
        var host = await OpenAsync(new HostFakeEngine());
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "session deadline", Timeout = TimeSpan.FromMilliseconds(100) }
        );
        var timeout = await Assert.ThrowsAsync<TestException>(() =>
            Expect.Poll(() => "running", Every(5, TimeSpan.FromSeconds(30))).ToBeAsync("done")
        );
        Assert.Equal("TEST_TIMEOUT", timeout.Code);
        Assert.Equal("session timed out after 100 ms", timeout.Message);

        var failures = await session.CloseAsync();
        Assert.Equal(2, failures.Count);
        Assert.Equal("TEST_TIMEOUT", Assert.IsAssignableFrom<E2EException>(failures[0]).Code);
        var secondary = Assert.IsAssignableFrom<E2EException>(failures[1]);
        Assert.Equal("ASSERTION_FAILED", secondary.Code);
        Assert.Matches(
            @"\Aexpect\.poll\(\.\.\.\)\.toBe\(\.\.\.\) stopped at the attempt deadline after \d+ ms\nlast: received ""running"", expected ""done""\z",
            secondary.Message
        );
        var recorded = Assert.Single(session.Report()!.Run.Results[0].Attempts[0].SecondaryErrors);
        Assert.Equal(secondary.Message, recorded.Message);
        Assert.Equal(E2E.Reports.ErrorPhase.Body, recorded.Phase);
        Assert.Contains("· timed out ·", await File.ReadAllTextAsync(session.TracePath));

        var next = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "after timeout", Timeout = TimeSpan.FromSeconds(5) }
        );
        await Expect.Poll(() => true, null).ToBeAsync(true);
        Assert.Empty(await next.CloseAsync());
    }

    [Theory]
    [InlineData("session signal")]
    [InlineData("host signal")]
    [InlineData("session close")]
    [InlineData("host close")]
    public async Task Cancels_an_in_flight_poll_on_(string cancellation)
    {
        using var controller = new CancellationTokenSource();
        var engine = new HostFakeEngine();
        var host = await OpenAsync(
            engine,
            cancellationToken: cancellation == "host signal" ? controller.Token : default
        );
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = cancellation, Timeout = TimeSpan.FromSeconds(30) },
            cancellation == "session signal" ? controller.Token : default
        );
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var poll = Expect
            .Poll(
                _ =>
                {
                    reading.TrySetResult();
                    return new TaskCompletionSource<bool>().Task;
                },
                new PollOptions { Timeout = TimeSpan.FromSeconds(30) }
            )
            .ToBeAsync(true);
        await reading.Task.WaitAsync(TimeSpan.FromSeconds(30));
        if (cancellation.EndsWith("signal", StringComparison.Ordinal))
        {
            await controller.CancelAsync();
        }
        else if (cancellation == "host close")
        {
            await host.CloseAsync();
        }
        else
        {
            await session.CloseAsync();
        }

        // Upstream maps a rejection to "cancelled" and a pass to "passed"; the poll here must reject, and not as a timeout.
        var error = await Assert.ThrowsAnyAsync<Exception>(() => poll.WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.IsNotType<TestException>(error);
        await session.CloseAsync();
        Assert.Equal(1, engine.Disposes);
    }

    [Fact]
    public async Task Partial_Refuses_test_setTimeout_in_a_hosted_session_which_runs_no_e2e_test()
    {
        File.WriteAllText(Path.Combine(_dir, E2EConfig.FileName), Config);
        var host = await E2EHost.OpenAsync(
            new OpenHostOptions
            {
                Cwd = _dir,
                Env = NoEnv,
                EngineFactory = _ => new HostFakeEngine(),
            }
        );
        _hosts.Add(host);
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "hosted setTimeout", Timeout = TimeSpan.FromSeconds(5) }
        );

        var refused = Assert.Throws<TestException>(() =>
            E2E.Internal.TestTimeout.SetRunningTestTimeout(
                new PublishedAttempt { AttemptId = session.AttemptId },
                TimeSpan.FromSeconds(60)
            )
        );
        Assert.Equal("INVALID_ARGUMENT", refused.Code);
        Assert.Contains("a hosted session runs none", refused.Message);
        Assert.Empty(await session.CloseAsync());
    }

    [Fact]
    public async Task Partial_Caps_contributed_fixture_calls_at_the_session_deadline_and_refuses_dispatch_after_expiry()
    {
        var engine = new HostFakeEngine();
        var host = await OpenAsync(engine);
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "contributed deadline", Timeout = TimeSpan.FromMilliseconds(10) }
        );
        var gadget = engine.Gadget!;

        var slow = await Assert.ThrowsAsync<TestException>(() =>
            gadget.SlowAsync().WaitAsync(TimeSpan.FromSeconds(30))
        );
        Assert.Equal("TEST_TIMEOUT", slow.Code);
        var late = await Assert.ThrowsAsync<TestException>(() => gadget.PokeAsync("too late"));
        Assert.Equal("TEST_TIMEOUT", late.Code);
        Assert.Equal(["slow"], engine.FixtureCalls);
        Assert.Contains(session.Steps(), step =>
            step.Api == "gadget.slow"
            && step.Status == E2E.Reports.StepStatus.Failed
            && step.Error?.Code == "TEST_TIMEOUT"
        );
        await session.CloseAsync();
        Assert.Contains("timed out", await File.ReadAllTextAsync(session.TracePath));
    }

    [Fact]
    public async Task Partial_Keeps_the_session_deadline_when_a_contributed_operation_disables_its_own_timeout()
    {
        var engine = new HostFakeEngine();
        var host = await OpenAsync(engine);
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "disabled operation timeout", Timeout = TimeSpan.FromMilliseconds(15) }
        );

        var error = await Assert.ThrowsAsync<TestException>(() =>
            engine.Gadget!.HangAsync().WaitAsync(TimeSpan.FromSeconds(30))
        );
        Assert.Equal("TEST_TIMEOUT", error.Code);
        Assert.Contains(await session.CloseAsync(), failure => failure is E2EException { Code: "TEST_TIMEOUT" });
    }

    [Fact]
    public async Task Cuts_a_core_operation_the_engine_never_finishes_at_the_session_deadline()
    {
        var performing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new HostFakeEngine
        {
            OnPerform = (_, _) =>
            {
                performing.TrySetResult();
                return new TaskCompletionSource().Task;
            },
        };
        var host = await OpenAsync(engine);
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "hung engine", Timeout = TimeSpan.FromMilliseconds(100) }
        );
        var started = DateTimeOffset.UtcNow;
        // The wait is bounded here too, so a regression of the cut fails this test instead of hanging the run.
        var error = await Assert.ThrowsAsync<TestException>(() =>
            session.Screen.GetByRole("button", "Submit").ClickAsync().WaitAsync(TimeSpan.FromSeconds(30))
        );
        Assert.Equal("TEST_TIMEOUT", error.Code);
        Assert.True(performing.Task.IsCompletedSuccessfully, "the deadline must cut a call the engine started");
        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(5));
        var failures = await session.CloseAsync();
        Assert.Contains(failures, failure =>
            failure is E2EException { Code: "TEST_TIMEOUT" }
            && failure.Message.Contains("session timed out after 100 ms")
        );
        Assert.Contains("timed out", await File.ReadAllTextAsync(session.TracePath));
        var next = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "after a hung engine", Timeout = TimeSpan.FromSeconds(5) }
        );
        Assert.Empty(await next.CloseAsync());
    }

    /// <summary>
    /// Partial: upstream cancels a contributed fixture (<c>gadget.hang</c>) with <c>OpenSessionOptions.signal</c>; the port has no <c>extend</c>, so a core
    /// operation the engine never finishes is cancelled with the open token, which is the port's signal (#2041).
    /// </summary>
    [Fact]
    public async Task Partial_Records_a_cancelled_fixture_as_interrupted_and_retains_failure_only_video()
    {
        const string video =
            """{ "trace": "on", "video": "retain-on-failure", "targets": [{ "name": "kiosk", "platform": "web", "app": { "url": "http://127.0.0.1:4173/" } }] }""";
        var performing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new HostFakeEngine
        {
            Video = true,
            OnPerform = (_, token) =>
            {
                performing.TrySetResult();
                return Task.Delay(Timeout.Infinite, token);
            },
        };
        var host = await OpenAsync(engine, video);
        using var controller = new CancellationTokenSource();
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "cancelled operation" },
            controller.Token
        );
        var pending = session.Screen.GetByRole("button", "Submit").ClickAsync();
        await performing.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await controller.CancelAsync();
        // The call fails as CANCELLED or surfaces the cancellation itself, depending on which the cut reaches first; never as a timeout.
        var error = await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.True(error is OperationCanceledException or E2EException { Code: "CANCELLED" }, error.ToString());
        Assert.Equal(E2E.Reports.StepStatus.Cancelled, session.Steps()[^1].Status);
        await session.CloseAsync();
        Assert.True(File.Exists(Path.Combine(session.ArtifactsDir, "video", "fake.webm")));
        var trace = await File.ReadAllTextAsync(session.TracePath);
        Assert.Contains("interrupted", trace);
        Assert.Contains("CANCELLED", trace);
        Assert.DoesNotContain("passed", trace.Split('\n')[0]);
    }

    [Fact]
    public async Task Records_a_blocked_agent_verdict_as_non_passing_and_retains_failure_only_video()
    {
        var config = E2EConfig
            .Parse(
                """{ "video": "retain-on-failure", "cache": { "mode": "off" }, "targets": [{ "name": "kiosk", "platform": "web", "app": { "url": "http://127.0.0.1:4173/" } }] }""",
                _dir
            )
            .WithExecutor(
                new E2E.Tests.AgentAct.TestExecutor("blocked-host-executor", (_, _) =>
                    Task.FromResult(
                        E2E.Tests.AgentAct.ExecutorKit.Blocked("cannot automate this surface", "AUTOMATION_UNSUPPORTED")
                    )
                )
            );
        var host = await E2EHost.OpenAsync(
            new OpenHostOptions
            {
                Cwd = _dir,
                Config = config,
                Env = NoEnv,
                EngineFactory = _ => new HostFakeEngine { Video = true },
            }
        );
        _hosts.Add(host);
        var session = await host.OpenSessionAsync(new OpenSessionOptions { Name = "blocked automation" });

        var error = await Assert.ThrowsAsync<AgentException>(() => session.Agent.ActAsync("unsupported operation"));
        Assert.Equal("AUTOMATION_UNSUPPORTED", error.Code);
        Assert.Equal(E2E.Reports.StepStatus.Blocked, session.Steps()[^1].Status);
        await session.CloseAsync();
        Assert.True(File.Exists(Path.Combine(session.ArtifactsDir, "video", "fake.webm")));
        var trace = await File.ReadAllTextAsync(session.TracePath);
        Assert.Contains("AUTOMATION_UNSUPPORTED", trace);
        Assert.DoesNotContain("passed", trace.Split('\n')[0]);
    }

    [Fact]
    public async Task Partial_Refuses_a_session_timeout_that_is_not_positive_and_finite()
    {
        var engine = new HostFakeEngine();
        var host = await OpenAsync(engine);
        foreach (var timeout in new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(-1) })
        {
            var error = await Assert.ThrowsAsync<ConfigurationException>(() =>
                host.OpenSessionAsync(new OpenSessionOptions { Name = "bad timeout", Timeout = timeout })
            );
            Assert.Equal("INVALID_CONFIG", error.Code);
        }

        Assert.Equal(0, engine.Starts);
        var session = await host.OpenSessionAsync(new OpenSessionOptions { Name = "default timeout" });
        Assert.Empty(await session.CloseAsync());
    }

    [Fact]
    public async Task Times_out_a_session_closed_past_its_deadline_before_the_deadline_timer_ran()
    {
        // The session's timer and deadline read a fake clock, so the 20 ms deadline passes while its timer has not fired, as upstream's busy loop holds it.
        var clock = new FakeTimeProvider();
        File.WriteAllText(Path.Combine(_dir, E2EConfig.FileName), Config);
        var host = await E2EHost.OpenAsync(
            new OpenHostOptions
            {
                Cwd = _dir,
                Env = NoEnv,
                EngineFactory = _ => new HostFakeEngine(),
                Clock = clock,
            }
        );
        _hosts.Add(host);
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "closed past deadline", Timeout = TimeSpan.FromMilliseconds(20) }
        );
        // AdjustTime moves the clock without running the timer that is due, which is the state upstream's busy loop leaves.
        clock.AdjustTime(clock.GetUtcNow() + TimeSpan.FromMilliseconds(21));

        var failures = await session.CloseAsync();
        var failure = Assert.IsAssignableFrom<E2EException>(Assert.Single(failures));
        Assert.Equal("TEST_TIMEOUT", failure.Code);
        Assert.Contains("session timed out after 20 ms", failure.Message);
        Assert.Contains("· timed out ·", await File.ReadAllTextAsync(session.TracePath));
    }

    /// <summary>.NET-only: upstream has no <c>it</c> for the caller's cancel (its <c>signal</c> option), which the port passes as the open token (#2002).</summary>
    [Fact]
    public async Task A_caller_cancel_stays_a_cancellation_and_is_not_reported_as_a_timeout_on_close()
    {
        var host = await OpenAsync(new HostFakeEngine());
        using var caller = new CancellationTokenSource();
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "caller cancel", Timeout = TimeSpan.FromMilliseconds(100) },
            caller.Token
        );
        await caller.CancelAsync();
        await Task.Delay(200);

        var failures = await session.CloseAsync();
        Assert.DoesNotContain(failures, failure => failure is E2EException { Code: "TEST_TIMEOUT" });
    }
}
