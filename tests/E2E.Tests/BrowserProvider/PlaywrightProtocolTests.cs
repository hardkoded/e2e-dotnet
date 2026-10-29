// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Tests.BrowserProvider;

/// <summary>
/// A lease whose <see cref="BrowserLease.Protocol"/> is <see cref="BrowserProtocol.Playwright"/>: the engine dials it with
/// Playwright's own protocol and opens the browser context itself. .NET-only: upstream's lease always carries a CDP endpoint.
/// The real tests use a local Playwright server as the grid.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class PlaywrightProtocolTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private const string Probe = """
        <!doctype html>
        <html><head><title>Probe</title></head>
        <body><h1></h1><script>
        document.querySelector('h1').textContent = [
          navigator.language,
          Intl.DateTimeFormat().resolvedOptions().timeZone,
          matchMedia('(prefers-color-scheme: dark)').matches,
          innerWidth,
          innerHeight,
        ].join('|');
        </script></body></html>
        """;

    private static BrowserLease Lease(string id, string endpoint) => new() { Id = id, CdpEndpoint = endpoint, Protocol = BrowserProtocol.Playwright };

    private static async Task<string?> HeadingAsync(IEngineSession session) =>
        WebSemanticsTests.Flatten((await session.ObserveAsync(None)).Roots).First(node => node.Role == "heading").Name;

    /// <summary>Returns once a read of the session fails, which is when its browser connection has seen the grid go.</summary>
    private static async Task WaitUntilDroppedAsync(IEngineSession session)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            try
            {
                await session.ObserveAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                throw new TimeoutException("the engine never noticed the grid drop the session");
            }
            catch (Exception)
            {
                return;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact]
    public async Task Connects_with_the_playwright_protocol_opens_its_own_context_with_the_engines_options_and_closes_the_connection_at_the_end()
    {
        using var app = await TinySite.StartAsync(Probe);
        await using var grid = await RemotePlaywrightServer.StartAsync();
        var cloud = new ToyCloud { OnAcquire = (request, token) => Task.FromResult(Lease("session-1", grid.Endpoint)) };
        var engine = new WebEngine(new WebEngineOptions
        {
            Browser = cloud,
            Locale = "de-DE",
            TimezoneId = "Asia/Tokyo",
            ColorScheme = WebColorScheme.Dark,
            Viewport = new WebViewport(412, 300),
        });

        await using (var session = await engine.StartAsync(new EngineStartOptions { BaseUrl = app.Url, ActionTimeout = TimeSpan.FromSeconds(30) }, None))
        {
            await session.OpenAsync(app.Url, None);
            Assert.Equal("de-DE|Asia/Tokyo|true|412|300", await HeadingAsync(session));
        }

        // The attempt's lease goes back, and the grid itself is the provider's to end, not the engine's.
        Assert.Equal(["session-1"], cloud.ReleasedIds);
        Assert.True(grid.Running);
    }

    [Fact]
    public async Task Replaces_a_dropped_playwright_browser_with_a_new_lease_at_the_next_attempt()
    {
        using var app = await TinySite.StartAsync(Probe);
        var grids = new List<RemotePlaywrightServer>();
        try
        {
            var cloud = new ToyCloud
            {
                OnAcquire = async (request, token) =>
                {
                    var grid = await RemotePlaywrightServer.StartAsync();
                    grids.Add(grid);
                    return Lease($"session-{grids.Count}", grid.Endpoint);
                },
            };
            var (runner, env) = await Runner.PreparedAsync(cloud, 1);
            var worker = new WebEngine(new WebEngineOptions { Browser = cloud, Locale = "fr-FR", Viewport = new WebViewport(412, 300) });
            worker.Init(Runner.InitInfo(0, env));
            var options = new EngineStartOptions { AttemptId = "a", BaseUrl = app.Url, ActionTimeout = TimeSpan.FromSeconds(30) };

            // The grid drops the session while the attempt rides it.
            var first = await worker.StartAsync(options, None);
            await first.OpenAsync(app.Url, None);
            await grids[0].KillAsync();

            // The engine's connection learns of the drop on its own time. A dispose that beats it counts the browser as still
            // held, and the worker would dial the dead grid again instead of leasing a new one (ECONNREFUSED).
            await WaitUntilDroppedAsync(first);
            await first.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Single(grids);

            await using (var second = await worker.StartAsync(options, None))
            {
                await second.OpenAsync(app.Url, None);
                Assert.StartsWith("fr-FR|", await HeadingAsync(second), StringComparison.Ordinal);
            }

            Assert.Equal(2, grids.Count);
            await worker.DisposeAsync();
            await runner.FinishAsync(Runner.FinishInfo(), None);
            Assert.Equal(["session-2", "session-1"], cloud.ReleasedIds);
        }
        finally
        {
            foreach (var grid in grids)
            {
                await grid.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Dials_a_playwright_lease_with_connect_and_a_cdp_lease_with_connect_over_cdp()
    {
        var cloud = new ToyCloud { OnAcquire = (request, token) => Task.FromResult(request.Slot == 0 ? Lease("pw", "wss://grid.example/pw") : new BrowserLease { Id = "cdp", CdpEndpoint = "wss://grid.example/cdp" }) };
        var dialer = new Dialer();
        var (runner, env) = await Runner.PreparedAsync(cloud, 2);

        // The protocol reaches the worker in the hand-off.
        var variable = Runner.LeasesVariableIn(env.Keys);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{"slots":2,"leases":[{"id":"pw","cdpEndpoint":"wss://grid.example/pw","protocol":"playwright"},{"id":"cdp","cdpEndpoint":"wss://grid.example/cdp"}]}"""),
            JsonNode.Parse(env[variable]!)));

        foreach (var slot in new[] { 0, 1 })
        {
            var worker = dialer.Engine(cloud);
            worker.Init(Runner.InitInfo(slot, env));
            await Runner.RunAttemptAsync(worker, "a" + slot);
        }

        Assert.Equal(["wss://grid.example/pw"], dialer.PlaywrightEndpoints);
        Assert.Equal(["wss://grid.example/cdp"], dialer.Endpoints);
        await runner.FinishAsync(Runner.FinishInfo(), None);
    }

    [Fact]
    public async Task Refuses_a_playwright_lease_in_attempt_scope_and_gives_it_back_because_that_connection_cannot_be_reattached()
    {
        var cloud = new ToyCloud(BrowserProviderScope.Attempt) { OnAcquire = (request, token) => Task.FromResult(Lease("pw", "wss://grid.example/pw")) };
        var dialer = new Dialer();

        var error = await Assert.ThrowsAsync<EngineException>(() => dialer.Engine(cloud).StartAsync(Runner.Attempt("a1"), None));

        Assert.Contains("cannot be reattached after a drop", error.Message, StringComparison.Ordinal);
        Assert.Equal(["pw"], cloud.ReleasedIds);
        Assert.Empty(dialer.PlaywrightEndpoints);
    }

    [Fact]
    public async Task Refuses_a_playwright_lease_that_names_a_reconnect_endpoint()
    {
        var cloud = new ToyCloud { OnAcquire = (request, token) => Task.FromResult(new BrowserLease { Id = "pw", CdpEndpoint = "wss://grid.example/pw", ReconnectEndpoint = "wss://grid.example/again", Protocol = BrowserProtocol.Playwright }) };

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Runner.PreparedAsync(cloud, 1));

        Assert.Contains("returned a lease without an id and a cdpEndpoint", error.Message, StringComparison.Ordinal);
    }
}
