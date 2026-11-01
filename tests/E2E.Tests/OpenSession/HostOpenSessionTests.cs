// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.OpenSession;

/// <summary>
/// Upstream's <c>describe('host.openSession')</c> (<c>tests/integration/open-session.test.ts</c>), through <see cref="E2EHost"/>. Upstream
/// writes <c>writeConfig</c> to a config file with a live engine; the port's <c>e2e.config.json</c> holds no engine, so a test writes the file
/// and hands the engine to the host as <see cref="OpenHostOptions.EngineFactory"/>. Its target is <c>web</c> where upstream's is <c>kiosk</c>.
/// The port's engine starts and is disposed once for each session, where upstream's worker initializes once and is disposed with the host.
/// </summary>
public sealed class HostOpenSessionTests : IDisposable
{
    private const string Config = """{ "trace": "on", "targets": [{ "name": "kiosk", "platform": "web", "app": { "url": "http://127.0.0.1:4173/" } }] }""";

    private static readonly IReadOnlyDictionary<string, string?> NoEnv = new Dictionary<string, string?>();

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-open-session-").FullName;
    private readonly List<E2EHost> _hosts = [];

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.CloseAsync().GetAwaiter().GetResult();
        }

        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Retains hosts so an assertion failure cannot leave a published session behind.</summary>
    private async Task<E2EHost> OpenAsync(HostFakeEngine engine, OpenHostOptions? options = null)
    {
        var host = await E2EHost.OpenAsync((options ?? new OpenHostOptions()) with
        {
            Cwd = _dir,
            Env = NoEnv,
            EngineFactory = _ => engine,
        });
        _hosts.Add(host);
        return host;
    }

    private void WriteConfig() => File.WriteAllText(Path.Combine(_dir, E2EConfig.FileName), Config);

    /// <summary>
    /// Partial: no <c>gadget</c> fixture (the port has no <c>extend</c>, #707), and <c>fixtures.platform</c> is the engine's <c>web</c>. The engine
    /// counts are the port's own: it starts once and is disposed once for the session, so the host's close disposes nothing more, where upstream's
    /// worker is disposed by the host's close (<c>disposes: 0</c> after the session, <c>1</c> after the host). The cleanup order is not checked.
    /// </summary>
    [Fact]
    public async Task Partial_Opens_the_target_with_its_fixtures_streams_steps_and_closes_each_owner_once()
    {
        WriteConfig();
        var fake = new HostFakeEngine();
        var progress = new List<Reports.StepProgress>();
        var host = await OpenAsync(fake);
        var session = await host.OpenSessionAsync(new OpenSessionOptions { Name = "home screen", OnStep = progress.Add });
        Assert.Equal("kiosk", session.Target);
        Assert.Equal("web", session.Fixtures.Platform);
        Assert.Equal(host.RunId, session.RunId);
        Assert.True(Path.IsPathRooted(session.ArtifactsDir));
        Assert.True(Path.IsPathRooted(session.TracePath));

        var shotPath = await session.Fixtures.App.ScreenshotAsync("home");
        Assert.Equal(Path.Combine(session.ArtifactsDir, "screenshots", "fake.png"), shotPath);
        Assert.True(File.Exists(shotPath));
        Assert.Single(progress, step => step.Phase == Reports.StepProgressPhase.Start);
        Assert.Single(progress, step => step.Phase == Reports.StepProgressPhase.End);
        var shot = Assert.Single(session.Steps(), step => step.Api == "app.screenshot");
        Assert.Equal(Reports.StepStatus.Passed, shot.Status);
        Assert.Single(shot.Artifacts);

        Assert.Empty(await session.CloseAsync());
        Assert.Empty(await session.CloseAsync());
        Assert.Contains("app.screenshot", await File.ReadAllTextAsync(session.TracePath), StringComparison.Ordinal);
        Assert.Equal(1, fake.Starts);
        Assert.Equal(1, fake.Disposes);
        Assert.Empty(await host.CloseAsync());
        Assert.Empty(await host.CloseAsync());
        Assert.Equal(1, fake.Disposes);
    }

    [Fact]
    public async Task Cancels_a_fixture_call_still_running_when_the_session_closes()
    {
        WriteConfig();
        var performing = new TaskCompletionSource();
        var fake = new HostFakeEngine
        {
            OnPerform = async (_, token) =>
            {
                performing.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            },
        };
        var host = await OpenAsync(fake);
        var session = await host.OpenSessionAsync(new OpenSessionOptions { Name = "cancel a click", Timeout = TimeSpan.FromSeconds(30) });
        var click = session.Fixtures.Screen.GetByRole("button", "Submit").TapAsync();
        var settled = click.ContinueWith(task => task.IsCompletedSuccessfully ? "resolved" : "rejected", TaskScheduler.Default);
        await performing.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var failures = await session.CloseAsync();
        Assert.Equal("rejected", await settled.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Contains(failures, failure => failure is E2EException { Code: "STEP_NOT_AWAITED" });
        Assert.Equal(1, fake.Disposes);

        // A step nobody awaited is a test outcome, not an unclean worker: the host opens the next session.
        await using var next = await host.OpenSessionAsync(new OpenSessionOptions { Name = "after the abandoned step" });
        Assert.Equal("kiosk", next.Target);
        await host.CloseAsync();
    }

    /// <summary>Partial: the engine counts are the port's (see the class), and its worker has no <c>dispose</c> of its own, so the host's close disposes nothing.</summary>
    [Fact]
    public async Task Partial_Returns_cleanup_failures_from_close_and_throws_them_from_session_disposal()
    {
        WriteConfig();
        var fake = new HostFakeEngine { OnDispose = () => Task.FromException(new InvalidOperationException("device stuck")) };
        var host = await OpenAsync(fake);
        var session = await host.OpenSessionAsync(new OpenSessionOptions { Name = "cleanup failure" });
        var failures = await session.CloseAsync();
        Assert.Contains("device stuck", string.Join(',', failures.Select(failure => failure.Message)), StringComparison.Ordinal);
        Assert.Same(failures, await session.CloseAsync());

        var disposed = await Assert.ThrowsAsync<AggregateException>(async () =>
        {
            await using var disposable = session;
            Assert.Equal("kiosk", disposable.Target);
        });
        Assert.NotEmpty(disposed.InnerExceptions);
        Assert.Equal((1, 1), (fake.Starts, fake.Disposes));
        var unsafeReuse = await Assert.ThrowsAsync<EngineException>(() => host.OpenSessionAsync(new OpenSessionOptions { Name = "unsafe reuse" }));
        Assert.Equal("WORKER_PROTOCOL", unsafeReuse.Code);
    }

    [Fact]
    public async Task Opens_a_config_value_resolved_against_cwd_with_AsyncDisposable_sessions()
    {
        var fake = new HostFakeEngine();
        await using (var host = await OpenAsync(fake, new OpenHostOptions { Config = E2EConfig.Parse(Config, _dir) }))
        {
            await using (var session = await host.OpenSessionAsync(new OpenSessionOptions { Name = "config value" }))
            {
                Assert.Equal("kiosk", session.Target);
                Assert.StartsWith(_dir + Path.DirectorySeparatorChar, session.ArtifactsDir, StringComparison.Ordinal);
                Assert.Equal(_dir, fake.Started!.ProjectRoot);
                Assert.Equal(1, fake.Starts);
            }

            Assert.Equal(1, fake.Disposes);
        }
    }

    /// <summary>
    /// Partial: the config declares one target (several are #647), so <c>TARGET_REQUIRED</c> is not reachable through a config and is checked on
    /// <see cref="SelectTarget"/> in <see cref="SelectTargetTests"/>. <c>UNKNOWN_TARGET</c> and opening the named target are checked, and that the
    /// engine of a target that was not opened is never built.
    /// </summary>
    [Fact]
    public async Task Partial_Needs_a_target_when_the_config_declares_several_and_opens_the_one_named()
    {
        WriteConfig();
        var built = new List<string>();
        var fake = new HostFakeEngine();
        var unknown = await Assert.ThrowsAsync<ConfigurationException>(() => E2EHost.OpenAsync(new OpenHostOptions
        {
            Cwd = _dir,
            Env = NoEnv,
            Target = "three",
            EngineFactory = target => { built.Add(target.Name); return fake; },
        }));
        Assert.Equal("UNKNOWN_TARGET", unknown.Code);
        Assert.Empty(built);

        await using var host = await OpenAsync(fake, new OpenHostOptions { Target = "kiosk" });
        await using var session = await host.OpenSessionAsync(new OpenSessionOptions { Name = "named target" });
        Assert.Equal("kiosk", session.Target);
        Assert.Equal(1, fake.Starts);
    }
}
