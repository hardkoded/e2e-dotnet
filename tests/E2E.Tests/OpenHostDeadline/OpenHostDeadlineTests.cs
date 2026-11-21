// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.OpenSession;

namespace E2E.Tests.OpenHostDeadline;

/// <summary>
/// Upstream's <c>describe('openHost: deadline')</c> (<c>tests/integration/open-host-deadline.test.ts</c>). The <c>it</c>s that need no feature the port
/// lacks are ported here. Not ported (hardkoded/e2e-dotnet#2002): the polls (<c>cuts a core operation the engine never finishes</c>: the port's session awaits an engine call that ignores its token; <c>bounds polls by config assertionTimeout</c>, <c>caps an explicitly long poll</c>,
/// <c>cancels an in-flight poll on %s</c>, <c>records abandoned polls</c>: the port's <c>Expect.Poll</c> takes no ambient session deadline), the contributed
/// fixture calls (no <c>extend</c>), and the signals (<c>OpenSessionOptions</c> and <c>OpenHostOptions</c> have no <c>signal</c>).
/// The <c>Partial_</c> <c>it</c>s: <c>refuses a session timeout</c> lacks the NaN and infinite cases (a <see cref="TimeSpan"/> has neither), <c>times out a session closed past its deadline</c> cannot hold the timer callback pending (see its comment), and the <c>setTimeout</c> one: upstream calls the global <c>test.setTimeout</c>, which finds the session through <c>globalThis</c>, where the port's
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
            E2E.Internal.TestTimeout.SetRunningTestTimeout(session, TimeSpan.FromSeconds(60))
        );
        Assert.Equal("INVALID_ARGUMENT", refused.Code);
        Assert.Contains("a hosted session runs none", refused.Message);
        Assert.Empty(await session.CloseAsync());
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
    public async Task Partial_Times_out_a_session_closed_past_its_deadline_before_the_deadline_timer_ran()
    {
        var host = await OpenAsync(new HostFakeEngine());
        var session = await host.OpenSessionAsync(
            new OpenSessionOptions { Name = "closed past deadline", Timeout = TimeSpan.FromMilliseconds(20) }
        );
        // Partial: the busy wait blocks this thread only, so the deadline timer can run on another thread first; the port has no way to hold it pending.
        // The close reports the timeout from the elapsed deadline either way.
        var busyUntil = DateTimeOffset.UtcNow.AddMilliseconds(60);
        while (DateTimeOffset.UtcNow < busyUntil) { }

        var failures = await session.CloseAsync();
        var failure = Assert.IsAssignableFrom<E2EException>(Assert.Single(failures));
        Assert.Equal("TEST_TIMEOUT", failure.Code);
        Assert.Contains("session timed out after 20 ms", failure.Message);
        Assert.Contains("· timed out ·", await File.ReadAllTextAsync(session.TracePath));
    }
}
