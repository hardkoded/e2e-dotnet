// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.BrowserProvider;

/// <summary>
/// <c>describe('web({ browser: provider })')</c> of upstream <c>browser-provider.test.ts</c>. The port's provider is an
/// interface, so an upstream check that a method exists has no counterpart.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class WebBrowserProviderTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public void Rejects_a_provider_without_a_name_the_two_methods_or_a_known_scope_PARTIAL_the_interface_requires_the_two_methods()
    {
        Assert.Contains("non-empty Name", Refused(new NamedProvider("")), StringComparison.Ordinal);
        Assert.Contains("provider \"x\" has scope 7; use Worker or Attempt", Refused(new NamedProvider("x", (BrowserProviderScope)7)), StringComparison.Ordinal);
        _ = new WebEngine(new WebEngineOptions { Browser = new ToyCloud(BrowserProviderScope.Attempt) });
    }

    [Fact]
    public void Rejects_downloads_that_are_not_a_directory_and_a_read_or_whose_dir_is_relative_and_takes_an_absolute_POSIX_or_Windows_dir_PARTIAL_null_downloads_are_none()
    {
        static Task<byte[]> Read(BrowserLease lease, string file, BrowserDownloadContext context, CancellationToken token) => Task.FromResult(Array.Empty<byte>());
        var empty = new NamedProvider("toy-cloud") { Downloads = new BrowserProviderDownloads { Dir = "", ReadAsync = Read } };
        var unread = new NamedProvider("toy-cloud") { Downloads = new BrowserProviderDownloads { Dir = "/downloads", ReadAsync = null! } };
        Assert.Contains("provider \"toy-cloud\" has downloads that are not a Dir and a ReadAsync", Refused(empty), StringComparison.Ordinal);
        Assert.Contains("provider \"toy-cloud\" has downloads that are not a Dir and a ReadAsync", Refused(unread), StringComparison.Ordinal);
        _ = new WebEngine(new WebEngineOptions { Browser = new NamedProvider("toy-cloud") { Downloads = new BrowserProviderDownloads { Dir = "/downloads", ReadAsync = Read } } });
        Assert.Contains(
            "provider \"toy-cloud\" has Downloads.Dir \"downloads\"; name an absolute path on the browser's machine",
            Refused(new NamedProvider("toy-cloud") { Downloads = new BrowserProviderDownloads { Dir = "downloads", ReadAsync = Read } }),
            StringComparison.Ordinal);
        _ = new WebEngine(new WebEngineOptions { Browser = new NamedProvider("toy-cloud") { Downloads = new BrowserProviderDownloads { Dir = "C:\\Users\\kernel\\Downloads", ReadAsync = Read } } });
    }

    [Fact]
    public void Rejects_a_provider_together_with_connect_two_browser_sources()
    {
        var refused = Assert.Throws<EngineException>(() => new WebEngine(new WebEngineOptions
        {
            Browser = new ToyCloud(),
            Connect = new WebConnectOptions { CdpEndpoint = _ => Task.FromResult("ws://x") },
        }));
        Assert.Equal("INVALID_CONFIG", refused.Code);
        Assert.Contains("two browser sources", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_creation_time_headers_credentials_and_user_agent_with_an_attempt_scoped_provider_as_persistent_connect_does()
    {
        var cloud = new ToyCloud(BrowserProviderScope.Attempt);
        var headers = new Dictionary<string, string> { ["x-preview"] = "synthetic" };
        Assert.Contains("persistent context", Refused(new WebEngineOptions { Browser = cloud, Headers = headers }), StringComparison.Ordinal);
        Assert.Contains("persistent context", Refused(new WebEngineOptions { Browser = cloud, BasicAuth = new WebBasicAuth("user", "synthetic") }), StringComparison.Ordinal);
        Assert.Contains("persistent context", Refused(new WebEngineOptions { Browser = cloud, UserAgent = "synthetic playwright" }), StringComparison.Ordinal);
        _ = new WebEngine(new WebEngineOptions { Browser = new ToyCloud(), Headers = headers });
    }

    [Fact]
    public async Task Declares_context_replacement_for_worker_scope_and_not_for_attempt_scope_and_a_finish_hook_for_both_PARTIAL_the_port_declares_no_capabilities()
    {
        using var app = await FixtureApp.StartAsync();
        await using var remote = await RemoteChrome.LaunchAsync();

        // Attempt scope rides one persistent context: clearing the state is refused, a restart is not.
        var perAttempt = new ToyCloud(BrowserProviderScope.Attempt) { OnAcquire = (request, token) => Task.FromResult(Lease(remote)) };
        var attempt = new WebEngine(new WebEngineOptions { Browser = perAttempt });
        await using (var session = await attempt.StartAsync(new EngineStartOptions { BaseUrl = app.Url }, None))
        {
            var error = await Assert.ThrowsAsync<EngineException>(() => session.ClearStateAsync(None));
            Assert.Equal("UNSUPPORTED_CAPABILITY", error.Code);
            Assert.Equal(
                "app.clearState() is unavailable with browser provider \"toy-cloud\" with scope \"attempt\": it replaces the browser context, and the attempt rides one persistent context; every attempt already starts on a fresh browser, and scope \"worker\" can clear one mid-test",
                error.Message);
            await session.RestartAsync(None);
        }

        Assert.Equal(["lease-1"], perAttempt.ReleasedIds);

        // Worker scope replaces the context, and finishes the run it leased for.
        var perWorker = new ToyCloud { OnAcquire = (request, token) => Task.FromResult(Lease(remote)) };
        var worker = new WebEngine(new WebEngineOptions { Browser = perWorker });
        var prepared = await worker.PrepareAsync(Runner.PrepareInfo(1), None);
        worker.Init(Runner.InitInfo(0, Runner.Widen(prepared.Env)));
        await using (var session = await worker.StartAsync(new EngineStartOptions { BaseUrl = app.Url }, None))
        {
            await session.ClearStateAsync(None);
        }

        await worker.FinishAsync(Runner.FinishInfo(), None);
        Assert.Equal(["lease-1"], perWorker.ReleasedIds);
    }

    private static BrowserLease Lease(RemoteChrome remote) => new() { Id = "lease-1", CdpEndpoint = remote.Endpoint };

    private static string Refused(IBrowserProvider provider) => Refused(new WebEngineOptions { Browser = provider });

    private static string Refused(WebEngineOptions options)
    {
        var error = Assert.Throws<EngineException>(() => new WebEngine(options));
        Assert.Equal("INVALID_CONFIG", error.Code);
        return error.Message;
    }

    /// <summary>A provider with the members a test sets and nothing else, to hand the engine what the scripted one never would.</summary>
    private sealed class NamedProvider(string name, BrowserProviderScope scope = BrowserProviderScope.Worker) : IBrowserProvider
    {
        public string Name => name;

        public BrowserProviderScope Scope => scope;

        public BrowserProviderDownloads? Downloads { get; init; }

        public Task<BrowserLease> AcquireAsync(BrowserRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ReleaseAsync(BrowserLease lease, BrowserReleaseContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
