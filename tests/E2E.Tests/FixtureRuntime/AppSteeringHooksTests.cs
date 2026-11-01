// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.FixtureRuntime;

public sealed class AppSteeringHooksTests
{
    [Fact]
    public async Task Restart_and_clearState_run_the_hook_then_reopen_the_app_at_its_base_URL()
    {
        var calls = new List<string>();
        await using var session = await StartAsync(calls, "http://127.0.0.1:4599");
        await session.App.RestartAsync();
        await session.App.ClearStateAsync();
        Assert.Equal(["restart", "open http://127.0.0.1:4599/", "reset", "open http://127.0.0.1:4599/"], calls);
        session.Complete();
    }

    [Fact]
    public async Task BaseUrl_has_no_trailing_slash_so_a_template_joins_a_path_onto_it_app_open_still_resolves_against_the_base_path()
    {
        await using (var origin = await StartAsync([], "http://127.0.0.1:4599"))
        {
            Assert.Equal("http://127.0.0.1:4599", origin.App.BaseUrl);
            origin.Complete();
        }

        var calls = new List<string>();
        await using var nested = await StartAsync(calls, "http://127.0.0.1:4599/app/");
        Assert.Equal("http://127.0.0.1:4599/app", nested.App.BaseUrl);
        Assert.Equal("http://127.0.0.1:4599/app/api/hooks", $"{nested.App.BaseUrl}/api/hooks");
        await nested.App.OpenAsync();
        await nested.App.OpenAsync("settings");
        Assert.Equal(["open http://127.0.0.1:4599/app/", "open http://127.0.0.1:4599/app/settings"], calls);
        nested.Complete();

        await using var none = await StartAsync([], baseUrl: null);
        Assert.Null(none.App.BaseUrl);
        none.Complete();
    }

    [Fact]
    public async Task Reopen_nothing_on_a_surface_without_an_address()
    {
        var calls = new List<string>();
        await using var session = await StartAsync(calls, baseUrl: null);
        await session.App.RestartAsync();
        await session.App.ClearStateAsync();
        Assert.Equal(["restart", "reset"], calls);
        session.Complete();
    }

    [Fact]
    public async Task App_open_on_a_device_launches_the_pinned_app_fresh_through_restart_as_one_app_open_step()
    {
        var calls = new List<string>();
        await using var session = await StartAsync(calls, baseUrl: null, device: true);
        await session.App.OpenAsync();
        Assert.Equal(["restart"], calls);
        Assert.Equal([("app.open", StepStatus.Passed)], session.StepRecords.Select(step => (step.Api, step.Status)));
        session.Complete();
    }

    [Fact]
    public async Task App_open_path_on_a_device_is_APP_URL_REQUIRED_and_names_device_openLink()
    {
        var calls = new List<string>();
        await using var session = await StartAsync(calls, baseUrl: null, device: true);
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => session.App.OpenAsync("/orders"));
        Assert.Equal("APP_URL_REQUIRED", error.Code);
        Assert.Contains("device.openLink", error.Message, StringComparison.Ordinal);
        Assert.Empty(calls);
        session.Complete();
    }

    // Upstream also has an it for an engine with no relaunch hook (UNSUPPORTED_CAPABILITY, "does not relaunch a pinned app"). It is not ported:
    // the port's PinnedApp capability stands for that hook, so there is no pinned engine without one.
    [Fact]
    public async Task App_open_keeps_the_engines_own_reason_it_cannot_launch()
    {
        const string Reason = "launching the app needs the build installed first with device.installApp()";
        await using var session = await StartAsync([], baseUrl: null, device: true, restartFailure: new EngineException("UNSUPPORTED_CAPABILITY", Reason));
        var error = await Assert.ThrowsAsync<EngineException>(() => session.App.OpenAsync());
        Assert.Equal("UNSUPPORTED_CAPABILITY", error.Code);
        Assert.Equal(Reason, error.Message);
        session.Complete();
    }

    private static Task<E2ESession> StartAsync(List<string> calls, string? baseUrl, bool device = false, Exception? restartFailure = null)
    {
        return E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new SteerableEngine(calls, device, restartFailure),
            BaseUrl = baseUrl,
            TestTitle = "steerable",
            CacheEnabled = false,
        });
    }

    private sealed class SteerableEngine(List<string> calls, bool device, Exception? restartFailure) : IEngine
    {
        public string Platform => "fake";

        public string Version => "1";

        public EngineCapabilities Capabilities => device ? EngineCapabilities.PinnedApp : EngineCapabilities.None;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
            Task.FromResult<IEngineSession>(new SteerableSession(calls, restartFailure));
    }

    private sealed class SteerableSession(List<string> calls, Exception? restartFailure) : IEngineSession
    {
        public string Route => "/";

        public Task OpenAsync(string url, CancellationToken cancellationToken)
        {
            calls.Add("open " + url);
            return Task.CompletedTask;
        }

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new Observation { Route = "/", Roots = [] });

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RestartAsync(CancellationToken cancellationToken)
        {
            if (restartFailure is not null)
            {
                return Task.FromException(restartFailure);
            }

            calls.Add("restart");
            return Task.CompletedTask;
        }

        public Task ClearStateAsync(CancellationToken cancellationToken)
        {
            calls.Add("reset");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
