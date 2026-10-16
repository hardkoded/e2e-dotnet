// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.BrowserProvider;

/// <summary>
/// .NET-only: no upstream <c>it</c> covers these. An engine started without a runner leases for the one attempt it starts (upstream
/// runs that through <c>standalone.ts</c>, which has no unit test of its own).
/// </summary>
public sealed class BrowserProviderTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task Leases_one_browser_for_an_engine_started_without_a_runner_and_releases_it_when_the_session_ends()
    {
        var cloud = new ToyCloud();
        var dialer = new Dialer();
        var engine = dialer.Engine(cloud);
        var session = await engine.StartAsync(Alone("a1"), None);
        var request = Assert.Single(cloud.Acquired);
        Assert.Equal((0, 1, (string?)null, "t"), (request.Slot, request.Slots, request.AttemptId, request.Env["BROWSER_TOKEN"]));
        Assert.Equal(["wss://0.example"], dialer.Endpoints);
        Assert.Empty(cloud.Released);
        await session.DisposeAsync();
        Assert.Equal(["lease-0"], cloud.ReleasedIds);
    }

    [Fact]
    public async Task Leases_a_browser_with_the_attempt_id_for_an_attempt_scoped_engine_started_without_a_runner()
    {
        var cloud = new ToyCloud(BrowserProviderScope.Attempt);
        var dialer = new Dialer();
        var engine = dialer.Engine(cloud);
        var session = await engine.StartAsync(Alone("a1"), None);
        Assert.Equal("a1", Assert.Single(cloud.Acquired).AttemptId);
        await session.DisposeAsync();
        Assert.Equal(["lease-0"], cloud.ReleasedIds);
    }

    [Fact]
    public async Task Starts_every_attempt_of_an_engine_without_a_runner_on_a_lease_of_its_own()
    {
        var cloud = new ToyCloud();
        var dialer = new Dialer();
        var engine = dialer.Engine(cloud);
        await (await engine.StartAsync(Alone("a1"), None)).DisposeAsync();
        await (await engine.StartAsync(Alone("a2"), None)).DisposeAsync();
        Assert.Equal(["wss://0.example", "wss://1.example"], dialer.Endpoints);
        Assert.Equal(["lease-0", "lease-1"], cloud.ReleasedIds);
    }

    [Fact]
    public async Task Gives_the_lease_back_when_the_start_of_an_engine_without_a_runner_fails_to_attach()
    {
        var cloud = new ToyCloud();
        var dialer = new Dialer { Failure = new Microsoft.Playwright.PlaywrightException("endpoint refused") };
        var engine = dialer.Engine(cloud);
        var error = await Assert.ThrowsAsync<EngineException>(() => engine.StartAsync(Alone("a1"), None));
        Assert.Equal("ENGINE_FAILURE", error.Code);
        Assert.Equal(["lease-0"], cloud.ReleasedIds);
    }

    [Fact]
    public async Task Holds_nothing_when_the_provider_refuses_the_lease_of_an_engine_without_a_runner()
    {
        var cloud = new ToyCloud(failSlot: 0);
        var dialer = new Dialer();
        var engine = dialer.Engine(cloud);
        var error = await Assert.ThrowsAsync<EngineException>(() => engine.StartAsync(Alone("a1"), None));
        Assert.Contains("could not lease a browser: no capacity for slot 0", error.Message, StringComparison.Ordinal);
        Assert.Empty(dialer.Endpoints);
        Assert.Empty(cloud.Released);
    }

    [Fact]
    public async Task Keeps_the_lease_prepare_made_when_an_attempt_fails_before_it_attaches()
    {
        var cloud = new ToyCloud();
        var dialer = new Dialer();
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var worker = dialer.Engine(cloud, [WebInitScript.FromPath("setup.js")]);
        worker.Init(Runner.InitInfo(0, env));
        var without = Directory.CreateTempSubdirectory("e2e-provider-root-").FullName;
        var with = Directory.CreateTempSubdirectory("e2e-provider-root-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(with, "setup.js"), "window.ready = true;", None);

            // The script is not under the first root, so the start fails before it asks for the endpoint.
            await Assert.ThrowsAsync<EngineException>(() => worker.StartAsync(new EngineStartOptions { AttemptId = "a1", ProjectRoot = without }, None));
            Assert.Empty(dialer.Endpoints);
            await (await worker.StartAsync(new EngineStartOptions { AttemptId = "a2", ProjectRoot = with }, None)).DisposeAsync();

            // Still the lease prepare made: a failed start that never attached did not call it dropped.
            Assert.Equal(["wss://0.example"], dialer.Endpoints);
            Assert.Single(cloud.Acquired);
        }
        finally
        {
            Directory.Delete(without, recursive: true);
            Directory.Delete(with, recursive: true);
        }
    }

    private static EngineStartOptions Alone(string attemptId) =>
        new() { AttemptId = attemptId, BaseUrl = "http://app.test/", ActionTimeout = TimeSpan.FromSeconds(1), Env = Runner.Token };
}
