// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.BrowserProvider;

/// <summary>
/// Ports the <c>it</c>s of <c>browser-provider.test.ts</c> that sit outside any <c>describe</c> (the CDP credentials), beside .NET-only tests that no upstream <c>it</c> covers.
/// .NET-only: no upstream <c>it</c> covers the others. An engine started without a runner leases for the one attempt it starts (upstream
/// runs that through <c>standalone.ts</c>, which has no unit test of its own), and <c>LeasedBrowsers.Downloads</c> is read by
/// the browser fixture's <c>waitForDownload</c> upstream, which the port does not have.
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

    [Fact]
    public async Task Offers_no_downloads_when_the_provider_has_none()
    {
        var leases = await WorkerAsync(new ToyCloud());
        Assert.Null(leases.Downloads());
    }

    [Fact]
    public async Task Reads_a_download_through_the_provider_for_the_lease_the_worker_holds()
    {
        var reads = new List<(string Lease, string File, string Run, string Target, string? Token)>();
        var cloud = new ToyCloud
        {
            Downloads = new BrowserProviderDownloads
            {
                Dir = "/tmp/e2e-downloads",
                ReadAsync = (lease, file, context, token) =>
                {
                    reads.Add((lease.Id, file, context.RunId, context.TargetName, context.Env["BROWSER_TOKEN"]));
                    return Task.FromResult<byte[]>([1, 2, 3]);
                },
            },
        };
        var downloads = (await WorkerAsync(cloud)).Downloads()!;
        Assert.Equal("/tmp/e2e-downloads", downloads.Dir);
        Assert.Equal("browser provider \"toy-cloud\"", downloads.Provider);
        Assert.Equal<byte[]>([1, 2, 3], await downloads.ReadAsync("/tmp/e2e-downloads/guid", None));
        Assert.Equal([("lease-0", "/tmp/e2e-downloads/guid", "run-1", "web", "t")], reads);
    }

    [Fact]
    public async Task Names_the_provider_when_a_download_cannot_be_read_and_reports_a_cancelled_read_as_CANCELLED()
    {
        var cloud = new ToyCloud
        {
            Downloads = new BrowserProviderDownloads
            {
                Dir = "/tmp/e2e-downloads",
                ReadAsync = (lease, file, context, token) => throw new InvalidOperationException("file API down"),
            },
        };
        var downloads = (await WorkerAsync(cloud)).Downloads()!;
        var failed = await Assert.ThrowsAsync<EngineException>(() => downloads.ReadAsync("/tmp/e2e-downloads/guid", None));
        Assert.Equal("ENGINE_FAILURE", failed.Code);
        Assert.Equal("browser provider \"toy-cloud\" could not read download /tmp/e2e-downloads/guid from browser lease-0: file API down", failed.Message);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        Assert.Equal("CANCELLED", (await Assert.ThrowsAsync<EngineException>(() => downloads.ReadAsync("/tmp/e2e-downloads/guid", cancelled.Token))).Code);
    }

    [Fact]
    public async Task Refuses_a_download_before_an_attempt_scoped_worker_has_a_browser()
    {
        var cloud = new ToyCloud(BrowserProviderScope.Attempt)
        {
            Downloads = new BrowserProviderDownloads { Dir = "/tmp/e2e-downloads", ReadAsync = (lease, file, context, token) => Task.FromResult<byte[]>([]) },
        };
        var downloads = (await WorkerAsync(cloud)).Downloads()!;
        var error = await Assert.ThrowsAsync<EngineException>(() => downloads.ReadAsync("/tmp/e2e-downloads/guid", None));
        Assert.Equal("INVALID_STATE", error.Code);
    }

    [Theory]
    [InlineData(BrowserProviderScope.Worker)]
    [InlineData(BrowserProviderScope.Attempt)]
    public async Task Resolves_CDP_credentials_from_the_worker_environment_in_scope(BrowserProviderScope scope)
    {
        var cloud = new ToyCloud(scope)
        {
            OnCdpHeaders = (_, context) => Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string> { ["Authorization"] = context.Env["WORKER_TOKEN"]! }),
        };
        var (runner, env) = await Runner.PreparedAsync(cloud, 1);
        Assert.Empty(cloud.CdpHeadersCalls);
        Assert.DoesNotContain("worker-secret", System.Text.Json.JsonSerializer.Serialize(env), StringComparison.Ordinal);
        var dialer = new Dialer();
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, new Dictionary<string, string?>(env) { ["WORKER_TOKEN"] = "worker-secret" }));
        await Runner.RunAttemptAsync(worker, "with-headers");
        var target = Assert.Single(dialer.Targets);
        Assert.Equal("wss://0.example", target.Endpoint);
        Assert.Equal(new Dictionary<string, string> { ["Authorization"] = "worker-secret" }, target.Headers);
        var (lease, request) = Assert.Single(cloud.CdpHeadersCalls);
        Assert.Equal("lease-0", lease.Id);
        Assert.Equal("worker-secret", request.Env["WORKER_TOKEN"]);
        await worker.DisposeAsync();
        await runner.FinishAsync(Runner.FinishInfo(), None);
    }

    [Fact]
    public async Task Accepts_endpoint_and_handshake_headers_together_from_a_connect_resolver()
    {
        var target = new CdpTarget { Endpoint = "wss://authenticated.example", Headers = new Dictionary<string, string> { ["Authorization"] = "synthetic-token" } };
        var dialed = new List<CdpTarget>();
        var worker = new WebEngine(new WebEngineOptions { Connect = new WebConnectOptions { CdpEndpoint = _ => Task.FromResult(target) } })
        {
            CreatePlaywright = AttemptSession.FakeRemote.Playwright,
            ConnectCdp = (_, connection, _, _) =>
            {
                dialed.Add(connection);
                return Task.FromResult(FakeBrowser.Ordinary().Browser);
            },
            RegisterCdpSelectors = (_, _, _) => Task.CompletedTask,
            Clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(),
        };
        await Runner.RunAttemptAsync(worker, "a1");
        Assert.Same(target, Assert.Single(dialed));
    }

    /// <summary>Partial: upstream probes with the internal <c>guard</c>; the port reconnects inside the next operation, here <c>OpenAsync</c>.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retains_attempt_CDP_credentials_on_reconnect_dedicated_endpoint_PARTIAL_the_probe_is_an_OpenAsync(bool reconnect)
    {
        var headers = new Dictionary<string, string> { ["Authorization"] = "synthetic-reconnect-token" };
        var cloud = new ToyCloud(BrowserProviderScope.Attempt, reconnect: reconnect)
        {
            OnCdpHeaders = (_, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(headers),
        };

        // One browser identity: recovery must land on the same persistent context.
        var dialer = new Dialer(_ => FakeBrowser.Persistent("same-browser"));
        var (runner, env) = await Runner.PreparedAsync(cloud, 1);
        var worker = dialer.Engine(cloud);
        worker.Init(Runner.InitInfo(0, env));
        var session = await worker.StartAsync(Runner.Attempt("a1"), None);
        await session.OpenAsync("http://app.test/", None);
        dialer.Browsers[0].Drop();
        await session.OpenAsync("http://app.test/", None);
        var recovered = dialer.Targets[1];
        Assert.Equal(reconnect ? "wss://0.example/reconnect" : "wss://0.example", recovered.Endpoint);
        Assert.Equal(headers, recovered.Headers);
        Assert.Equal(2, cloud.CdpHeadersCalls.Count);
        Assert.Single(cloud.Acquired);
        await session.DisposeAsync();
        await worker.DisposeAsync();
        await runner.FinishAsync(Runner.FinishInfo(), None);
    }

    private static EngineStartOptions Alone(string attemptId) =>
        new() { AttemptId = attemptId, BaseUrl = "http://app.test/", ActionTimeout = TimeSpan.FromSeconds(1), Env = Runner.Token };

    private static async Task<LeasedBrowsers> WorkerAsync(ToyCloud cloud)
    {
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var leases = new LeasedBrowsers(cloud);
        env["BROWSER_TOKEN"] = "t";
        leases.Init(Runner.InitInfo(0, env));
        return leases;
    }
}
