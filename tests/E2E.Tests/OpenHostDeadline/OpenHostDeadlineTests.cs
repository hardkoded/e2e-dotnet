// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.OpenSession;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Tests.OpenHostDeadline;

/// <summary>
/// Upstream's <c>describe('openHost: deadline')</c> (<c>tests/integration/open-host-deadline.test.ts</c>). The <c>it</c>s that need no feature the port
/// lacks are ported here. Not ported (hardkoded/e2e-dotnet#2002, #2041): <c>applies the project toPass timeout in a hosted session</c> (a hosted session publishes no attempt, so <c>ToPassAsync</c> cannot read the config's <c>toPass</c> there), the polls (<c>bounds polls by config assertionTimeout</c>, <c>caps an explicitly long poll</c>,
/// <c>cancels an in-flight poll on %s</c>, <c>records abandoned polls</c>: the port's <c>Expect.Poll</c> takes no ambient session deadline), and the signals (<c>OpenSessionOptions</c> and <c>OpenHostOptions</c> have no <c>signal</c>).
/// The <c>Partial_</c> <c>it</c>s: the two contributed-fixture <c>it</c>s (<c>caps contributed fixture calls...</c>, <c>keeps the session deadline when a contributed operation disables its own timeout</c>) reach the fixture through the fake engine's <c>Gadget</c> instead of <c>session.fixtures.gadget</c>, since a hosted session's <c>Fixtures</c> is the <c>TestContext</c> and the port's operations declare no timeout of their own (so <c>timeout: false</c> has nothing to disable); <c>refuses a session timeout</c> lacks the NaN and infinite cases (a <see cref="TimeSpan"/> has neither), <c>times out a session closed past its deadline</c> uses a 60 s timeout, not upstream's 20 ms (see its comment), and the <c>setTimeout</c> one: upstream calls the global <c>test.setTimeout</c>, which finds the session through <c>globalThis</c>, where the port's
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

    private async Task<E2EHost> OpenAsync(HostFakeEngine engine)
    {
        File.WriteAllText(Path.Combine(_dir, E2EConfig.FileName), Config);
        var host = await E2EHost.OpenAsync(
            new OpenHostOptions
            {
                Cwd = _dir,
                Env = NoEnv,
                EngineFactory = _ => engine,
            }
        );
        _hosts.Add(host);
        return host;
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

        var slow = await Assert.ThrowsAsync<TestException>(() => gadget.SlowAsync().WaitAsync(TimeSpan.FromSeconds(30)));
        Assert.Equal("TEST_TIMEOUT", slow.Code);
        var late = await Assert.ThrowsAsync<TestException>(() => gadget.PokeAsync("too late"));
        Assert.Equal("TEST_TIMEOUT", late.Code);
        Assert.Equal(["slow"], engine.FixtureCalls);
        Assert.Contains(session.Steps(), step =>
            step.Api == "gadget.slow" && step.Status == E2E.Reports.StepStatus.Failed && step.Error?.Code == "TEST_TIMEOUT"
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
