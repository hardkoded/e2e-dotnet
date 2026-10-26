// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Web;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.TestMu.Tests.Browsers;

/// <summary>
/// <see cref="TestMuBrowsers"/>: the endpoint and capabilities it builds, the credentials it reads from the run's
/// environment, and what it keeps out of progress lines. .NET-only: the port reaches the grid on the Playwright-protocol
/// endpoint. The ported upstream <c>it</c>s are in <see cref="TestMuBrowsersFunctionTests"/>.
/// </summary>
public sealed class TestMuBrowsersTests
{
    private static readonly IReadOnlyDictionary<string, string?> Credentials = new Dictionary<string, string?> { ["LT_USERNAME"] = "ada", ["LT_ACCESS_KEY"] = "secret key" };

    internal static BrowserRequest Request(IReadOnlyDictionary<string, string?>? env = null, string? attemptId = null, int slot = 0, int slots = 1) => new()
    {
        RunId = "run-a",
        TargetName = "web",
        Slot = slot,
        Slots = slots,
        AttemptId = attemptId,
        Env = env ?? Credentials,
        Log = _ => { },
    };

    internal static JsonObject Capabilities(BrowserLease lease)
    {
        Assert.StartsWith("wss://cdp.lambdatest.com/playwright?capabilities=", lease.CdpEndpoint, StringComparison.Ordinal);
        var query = HttpUtility.ParseQueryString(new Uri(lease.CdpEndpoint).Query);
        return JsonNode.Parse(query["capabilities"]!)!.AsObject();
    }

    [Fact]
    public async Task Leases_a_chrome_session_with_the_documented_capabilities()
    {
        var lease = await new TestMuBrowsers().AcquireAsync(Request(), CancellationToken.None);

        // The grid answers Playwright's protocol on this URL, not CDP: a live account refused the CDP attach.
        Assert.Equal(BrowserProtocol.Playwright, lease.Protocol);
        var capabilities = Capabilities(lease);
        Assert.Equal("Chrome", (string?)capabilities["browserName"]);
        Assert.Equal("latest", (string?)capabilities["browserVersion"]);
        var lt = capabilities["LT:Options"]!.AsObject();
        Assert.Equal("Windows 11", (string?)lt["platform"]);
        Assert.Equal("e2e", (string?)lt["project"]);
        Assert.Equal("e2e-run-a", (string?)lt["build"]);
        Assert.Equal(600, (int?)lt["idleTimeout"]);
        Assert.Equal("e2e-run-a-web-1", (string?)lt["name"]);
        Assert.Equal("ada", (string?)lt["user"]);
        Assert.Equal("secret key", (string?)lt["accessKey"]);
        Assert.Equal(["platform", "project", "build", "idleTimeout", "name", "user", "accessKey"], lt.Select(pair => pair.Key));
    }

    [Fact]
    public async Task Sends_the_options_it_was_given()
    {
        var provider = new TestMuBrowsers(new TestMuBrowsersOptions
        {
            BrowserName = "MicrosoftEdge",
            BrowserVersion = "130",
            Platform = "Windows 11",
            Build = "nightly",
            Video = true,
            Network = false,
            Console = true,
            TunnelName = "office",
            GeoLocation = "US",
            Capabilities = new Dictionary<string, JsonNode?> { ["resolution"] = "1920x1080" },
        });

        var capabilities = Capabilities(await provider.AcquireAsync(Request(), CancellationToken.None));

        Assert.Equal("MicrosoftEdge", (string?)capabilities["browserName"]);
        Assert.Equal("130", (string?)capabilities["browserVersion"]);
        var lt = capabilities["LT:Options"]!.AsObject();
        Assert.Equal("Windows 11", (string?)lt["platform"]);
        Assert.Equal("nightly", (string?)lt["build"]);
        Assert.True((bool?)lt["video"]);
        Assert.False((bool?)lt["network"]);
        Assert.True((bool?)lt["console"]);
        Assert.True((bool?)lt["tunnel"]);
        Assert.Equal("office", (string?)lt["tunnelName"]);
        Assert.Equal("US", (string?)lt["geoLocation"]);
        Assert.Equal("1920x1080", (string?)lt["resolution"]);
    }

    [Fact]
    public async Task Names_a_per_attempt_session_after_the_attempt()
    {
        var attempt = Capabilities(await new TestMuBrowsers().AcquireAsync(Request(attemptId: "a-7"), CancellationToken.None));

        Assert.Equal("e2e-run-a-web-attempt-a-7", (string?)attempt["LT:Options"]!["name"]);
    }

    [Fact]
    public async Task Reads_the_credentials_from_the_testmu_names_when_the_lt_names_are_unset()
    {
        var env = new Dictionary<string, string?> { ["TESTMU_USERNAME"] = "grace", ["TESTMU_ACCESS_KEY"] = "other", ["LT_USERNAME"] = "" };

        var lt = Capabilities(await new TestMuBrowsers().AcquireAsync(Request(env), CancellationToken.None))["LT:Options"]!;

        Assert.Equal("grace", (string?)lt["user"]);
        Assert.Equal("other", (string?)lt["accessKey"]);
    }

    [Fact]
    public async Task Keeps_the_access_key_out_of_the_lease_id()
    {
        var lease = await new TestMuBrowsers().AcquireAsync(Request(), CancellationToken.None);

        Assert.DoesNotContain("secret key", lease.Id, StringComparison.Ordinal);
        Assert.DoesNotContain("ada", lease.Id, StringComparison.Ordinal);
        Assert.Null(lease.ReconnectEndpoint);
    }

    [Fact]
    public async Task Stops_before_building_an_endpoint_when_cancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new TestMuBrowsers().AcquireAsync(Request(), cancelled.Token));
    }

    [Fact]
    public async Task Leases_per_worker_and_has_nothing_to_sweep()
    {
        IBrowserProvider provider = new TestMuBrowsers();

        Assert.Equal("testmu", provider.Name);
        Assert.Equal(BrowserProviderScope.Worker, provider.Scope);
        Assert.Empty(await provider.SweepAsync(new BrowserReleaseContext { RunId = "r", TargetName = "web", Env = Credentials, Log = _ => { } }, CancellationToken.None));
    }

    [Fact]
    public async Task Release_calls_nothing_and_can_run_twice()
    {
        var provider = new TestMuBrowsers();
        var lease = await provider.AcquireAsync(Request(), CancellationToken.None);
        var context = new BrowserReleaseContext { RunId = "run-a", TargetName = "web", Env = Credentials, Log = _ => { } };

        await provider.ReleaseAsync(lease, context, CancellationToken.None);
        await provider.ReleaseAsync(lease, context, CancellationToken.None);
    }

    [Fact]
    public async Task A_refused_connection_does_not_carry_the_access_key()
    {
        var lease = await new TestMuBrowsers().AcquireAsync(Request(), CancellationToken.None);
        var refused = lease.CdpEndpoint.Replace("wss://cdp.lambdatest.com", "ws://127.0.0.1:9", StringComparison.Ordinal);
        Assert.Contains("secret%20key", refused, StringComparison.Ordinal);
        using var playwright = await Playwright.CreateAsync();

        var error = await Assert.ThrowsAsync<PlaywrightException>(() => playwright.Chromium.ConnectAsync(refused, new BrowserTypeConnectOptions { Timeout = 5000 }));

        Assert.DoesNotContain("secret", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
