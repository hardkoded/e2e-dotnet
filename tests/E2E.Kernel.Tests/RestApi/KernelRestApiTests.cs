// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json.Nodes;
using E2E.Kernel.Tests.Kernel;
using static E2E.Kernel.Tests.Kernel.KernelFixtures;

namespace E2E.Kernel.Tests.RestApi;

/// <summary>
/// What replaces upstream's use of <c>@onkernel/sdk</c>: the REST requests the provider sends. .NET-only, since upstream's
/// tests mock the SDK and so cover none of this.
/// </summary>
public sealed class KernelRestApiTests
{
    [Fact]
    public async Task Calls_the_production_API_unless_the_run_environment_names_another()
    {
        var fake = new FakeKernel();
        var provider = Provider(fake);

        await provider.AcquireAsync(Request(), CancellationToken.None);
        await provider.AcquireAsync(Request(env: new Dictionary<string, string?> { ["KERNEL_API_KEY"] = "k", ["KERNEL_BASE_URL"] = "https://kernel.example/" }), CancellationToken.None);

        Assert.Equal(["https://api.onkernel.com/browsers", "https://kernel.example/browsers"], fake.Of("create").Select(call => call.Uri.GetLeftPart(UriPartial.Path)));
    }

    [Fact]
    public async Task Refuses_a_base_URL_that_is_not_http_or_https()
    {
        var fake = new FakeKernel();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Provider(fake).AcquireAsync(Request(env: new Dictionary<string, string?> { ["KERNEL_API_KEY"] = "k", ["KERNEL_BASE_URL"] = "ftp://kernel.example" }), CancellationToken.None));

        Assert.Equal("KERNEL_BASE_URL is not an http(s) URL", failure.Message);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task Sends_the_create_browser_fields_by_their_wire_names_and_lets_Extra_override_them()
    {
        var fake = new FakeKernel();
        var options = new KernelOptions
        {
            Stealth = true,
            Gpu = true,
            ProxyId = "proxy-1",
            Profile = new KernelBrowserProfile { Name = "qa", SaveChanges = true },
            Viewport = new KernelBrowserViewport { Width = 1920, Height = 1080, RefreshRate = 60 },
            TimeoutSeconds = 30,
            Extra = new JsonObject { ["region"] = "eu-west", ["timeout_seconds"] = 45 },
        };

        await Provider(fake, options).AcquireAsync(Request(), CancellationToken.None);

        var body = Assert.Single(fake.Of("create")).Body;
        AssertJson(
            new JsonObject
            {
                ["stealth"] = true,
                ["gpu"] = true,
                ["timeout_seconds"] = 45,
                ["proxy_id"] = "proxy-1",
                ["profile"] = new JsonObject { ["name"] = "qa", ["save_changes"] = true },
                ["viewport"] = new JsonObject { ["width"] = 1920, ["height"] = 1080, ["refresh_rate"] = 60 },
                ["tags"] = new JsonObject { ["e2e_run"] = "run-1", ["e2e_target"] = "web", ["e2e_slot"] = "0" },
                ["region"] = "eu-west",
            },
            body);
    }

    [Fact]
    public async Task Reads_the_message_of_a_JSON_error_body()
    {
        var fake = new FakeKernel { Create = () => FakeKernel.Json(new JsonObject { ["message"] = "quota exceeded" }, HttpStatusCode.TooManyRequests) };

        var failure = await Assert.ThrowsAsync<KernelApiException>(() => Provider(fake).AcquireAsync(Request(), CancellationToken.None));

        Assert.Equal("429 quota exceeded", failure.Message);
    }

    [Fact]
    public async Task Follows_the_pages_of_the_active_list_by_X_Next_Offset_and_stops_on_X_Has_More_false()
    {
        var fake = new FakeKernel();
        fake.List = uri =>
        {
            var first = !uri.Query.Contains("offset=", StringComparison.Ordinal);
            var page = FakeKernel.Json(new JsonArray(first ? [Item("b1"), Item("b2")] : [Item("b3")]));
            page.Headers.Add("X-Has-More", first ? "true" : "false");
            page.Headers.Add("X-Next-Offset", first ? "2" : "0");
            return page;
        };
        var client = new KernelClient(fake.Client, "k", new Uri("https://api.onkernel.com/"));

        var ids = await client.ListActiveAsync(new Dictionary<string, string> { ["e2e_run"] = "run-1" }, CancellationToken.None);

        Assert.Equal(["b1", "b2", "b3"], ids);
        Assert.Contains("offset=2", fake.Of("list").Last().Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_to_truncate_a_list_that_has_more_without_saying_where()
    {
        var fake = new FakeKernel();
        fake.List = _ =>
        {
            var page = FakeKernel.Json(new JsonArray(Item("b1")));
            page.Headers.Add("X-Has-More", "true");
            return page;
        };
        var client = new KernelClient(fake.Client, "k", new Uri("https://api.onkernel.com/"));

        await Assert.ThrowsAsync<KernelApiException>(() => client.ListActiveAsync(new Dictionary<string, string>(), CancellationToken.None));
    }

    private static JsonNode Item(string id) => new JsonObject { ["session_id"] = id };
}
