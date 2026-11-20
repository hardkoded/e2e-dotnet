// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.FetchMock;

namespace E2E.Decision.Tests.FetchWithValidatedRedirects;

/// <summary>The AI SDK's <c>fetch-with-validated-redirects.test.ts</c>, <c>describe('fetchWithValidatedRedirects')</c>.</summary>
public sealed class FetchWithValidatedRedirectsTests
{
    private static readonly int[] RedirectStatuses = [301, 302, 303, 307, 308];

    public static TheoryData<int> RedirectStatusData() => [.. RedirectStatuses];

    [Theory]
    [InlineData(null)]
    [InlineData("https://example.com")]
    [InlineData("https://provider.example.com")]
    public async Task Preserves_legacy_first_hop_credentials_and_custom_headers_with_trustedOrigin(
        string? trustedOrigin
    )
    {
        var fetch = new FetchMock().Once(Ok);

        await Sdk.FetchWithValidatedRedirects.FetchAsync(
            "https://example.com/file",
            new Dictionary<string, string>
            {
                ["authorization"] = "Bearer secret",
                ["x-jfrog-art-api"] = "vendor-secret",
                ["x-custom-metadata"] = "custom-value",
            },
            fetch.Fetch,
            CancellationToken.None,
            trustedOrigin
        );

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["authorization"] = "Bearer secret",
                ["x-jfrog-art-api"] = "vendor-secret",
                ["x-custom-metadata"] = "custom-value",
            },
            Lowered(fetch.Calls[0])
        );
    }

    [Fact]
    public async Task Validates_the_initial_URL_before_requesting_it()
    {
        var fetch = new FetchMock();

        await Assert.ThrowsAsync<DownloadException>(() => Fetch("http://localhost/file", fetch));
        Assert.Empty(fetch.Calls);
    }

    [Fact]
    public async Task Uses_redirect_manual_and_omits_headers_when_none_are_provided()
    {
        var fetch = new FetchMock().Once(Ok);

        await Fetch("https://example.com/file", fetch);

        var call = Assert.Single(fetch.Calls);
        Assert.Equal("https://example.com/file", call.Url);
        Assert.Equal(CancellationToken.None, call.Token);
        Assert.Empty(call.Headers);
    }

    [Fact]
    public async Task Follows_a_redirect_to_a_safe_URL_validating_the_hop()
    {
        var fetch = new FetchMock().Once(() => Redirect("https://cdn.example.com/file")).Once(Ok);

        using var response = await Fetch("https://example.com/file", fetch);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(2, fetch.Calls.Count);
        Assert.Equal("https://cdn.example.com/file", fetch.Calls[1].Url);
        Assert.Empty(fetch.Calls[1].Headers);
    }

    [Theory]
    [MemberData(nameof(RedirectStatusData))]
    public async Task Rejects_HTTPS_to_HTTP_downgrades_before_requesting_the_target(int status)
    {
        var body = new TrackingContent();
        var fetch = new FetchMock().Always(Ok).Once(() => Redirect("http://example.com/file", status, body));

        await Assert.ThrowsAsync<DownloadException>(() =>
            Fetch(
                "https://example.com/file",
                fetch,
                new Dictionary<string, string> { ["authorization"] = "Bearer secret" }
            )
        );
        Assert.Single(fetch.Calls);
        Assert.Equal(1, body.Disposed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://cdn.example.com")]
    public async Task Rejects_cross_host_HTTPS_downgrades_even_with_trustedOrigin_and_no_caller_headers(
        string? trustedOrigin
    )
    {
        var fetch = new FetchMock().Always(Ok).Once(() => Redirect("http://cdn.example.com/file"));

        await Assert.ThrowsAsync<DownloadException>(() =>
            Sdk.FetchWithValidatedRedirects.FetchAsync(
                "https://example.com/file",
                null,
                fetch.Fetch,
                CancellationToken.None,
                trustedOrigin
            )
        );
        Assert.Single(fetch.Calls);
    }

    [Fact]
    public async Task Rejects_a_downgrade_after_an_HTTP_request_upgrades_to_HTTPS()
    {
        var fetch = new FetchMock()
            .Always(Ok)
            .Once(() => Redirect("https://example.com/secure"))
            .Once(() => Redirect("http://example.com/file"));

        await Assert.ThrowsAsync<DownloadException>(() => Fetch("http://example.com/file", fetch));
        Assert.Equal(2, fetch.Calls.Count);
    }

    [Theory]
    [InlineData("http://example.com/next")]
    [InlineData("https://example.com/next")]
    public async Task Allows_an_initial_HTTP_URL_to_redirect_to(string location)
    {
        var fetch = new FetchMock().Once(() => Redirect(location)).Once(Ok);

        using var response = await Fetch("http://example.com/file", fetch);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(2, fetch.Calls.Count);
    }

    [Fact]
    public async Task Rejects_a_redirect_to_a_private_address_without_requesting_it()
    {
        var fetch = new FetchMock().Once(() => Redirect("https://169.254.169.254/"));

        await Assert.ThrowsAsync<DownloadException>(() => Fetch("https://evil.com/redirect", fetch));
        Assert.Single(fetch.Calls);
    }

    [Fact]
    public async Task Starts_cancelling_redirect_response_bodies_to_prevent_socket_leaks()
    {
        var first = new TrackingContent();
        var second = new TrackingContent();
        var fetch = new FetchMock()
            .Once(() => Redirect("https://cdn.example.com/file", body: first))
            .Once(() => Redirect("https://169.254.169.254/", body: second));

        await Assert.ThrowsAsync<DownloadException>(() => Fetch("https://example.com/file", fetch));

        // Both the followed hop and the hop rejected by the SSRF guard must have their bodies released so the chain does not leak sockets.
        Assert.Equal(1, first.Disposed);
        Assert.Equal(1, second.Disposed);
    }

    [Fact]
    public async Task Fails_closed_on_an_opaque_redirect_outside_the_browser()
    {
        var fetch = new FetchMock().Once(OpaqueRedirect);

        await Assert.ThrowsAsync<DownloadException>(() =>
            Sdk.FetchWithValidatedRedirects.FetchAsync(
                "https://example.com/redirect",
                null,
                fetch.Fetch,
                CancellationToken.None
            )
        );
        Assert.Single(fetch.Calls);
    }

    [Fact]
    public async Task Follows_redirects_natively_on_an_opaque_redirect_in_the_browser()
    {
        using var browser = IsBrowserRuntime.Simulate();
        var fetch = new FetchMock().Once(OpaqueRedirect).Once(Ok);

        using var response = await Sdk.FetchWithValidatedRedirects.FetchAsync(
            "https://example.com/file",
            null,
            fetch.Fetch,
            CancellationToken.None
        );

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(2, fetch.Calls.Count);
        Assert.Equal("https://example.com/file", fetch.Calls[1].Url);
        Assert.Equal("follow", fetch.Calls[1].Redirect);
    }

    [Theory]
    [MemberData(nameof(RedirectStatusData))]
    public async Task Partial_Does_not_wait_for_cancellation_before_following_a_redirect(int status)
    {
        // Upstream holds body.cancel() pending and checks the next fetch already ran. A .NET response body is released by a synchronous
        // Dispose, so there is no pending cancellation to wait for: this checks that the redirect is followed once the body was released.
        var body = new TrackingContent();
        var fetch = new FetchMock().Once(() => Redirect("https://cdn.example.com/file", status, body)).Once(Ok);

        using var response = await Fetch("https://example.com/file", fetch);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(2, fetch.Calls.Count);
        Assert.Equal(1, body.Disposed);
    }

    [Fact]
    public async Task Resolves_relative_redirect_targets_against_the_current_URL()
    {
        var fetch = new FetchMock().Once(() => Redirect("/internal")).Once(Ok);

        using var response = await Fetch("https://example.com/start", fetch);

        Assert.Equal("https://example.com/internal", fetch.Calls[1].Url);
    }

    [Theory]
    [MemberData(nameof(RedirectStatusData))]
    public async Task Follows_a_redirect(int status)
    {
        var fetch = new FetchMock().Once(() => Redirect("https://cdn.example.com/file", status)).Once(Ok);

        using var response = await Fetch("https://example.com/file", fetch);

        Assert.Equal(2, fetch.Calls.Count);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(304)]
    public async Task Returns_a_response_as_final_even_when_it_carries_a_Location_header(int status)
    {
        // Per the fetch spec, only 301/302/303/307/308 are redirect statuses. A hostile Location on a non-redirect status must not be
        // followed, not even to a private address.
        var fetch = new FetchMock().Once(() => Redirect("http://169.254.169.254/", status));

        using var response = await Fetch("https://example.com/file", fetch);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Single(fetch.Calls);
    }

    [Fact]
    public async Task Rejects_once_the_redirect_limit_is_exceeded()
    {
        var fetch = new FetchMock().Always(() => Redirect("https://example.com/next"));

        await Assert.ThrowsAsync<DownloadException>(() =>
            Sdk.FetchWithValidatedRedirects.FetchAsync(
                "https://example.com/start",
                null,
                fetch.Fetch,
                CancellationToken.None,
                maxRedirects: 2
            )
        );
    }

    [Fact]
    public async Task Passes_each_hop_an_independent_header_snapshot_later_hops_must_not_mutate_earlier_ones()
    {
        var fetch = new FetchMock().Once(() => Redirect("https://other.example.net/a")).Once(Ok);

        using var response = await Fetch(
            "https://example.com/file",
            fetch,
            new Dictionary<string, string> { ["authorization"] = "Bearer secret" }
        );

        var firstHop = fetch.Calls[0].Headers;
        var secondHop = fetch.Calls[1].Headers;
        Assert.NotSame(firstHop, secondHop);
        // The cross-origin credential drop on the second hop must not be visible in the header set the first hop was issued with.
        Assert.Equal("Bearer secret", firstHop["authorization"]);
        Assert.False(secondHop.ContainsKey("authorization"));
    }

    [Theory]
    [InlineData("http://localhost:5000")]
    [InlineData("https://service.internal")]
    public async Task Skips_validation_for_hops_same_origin_with_trustedOrigin(string trustedOrigin)
    {
        // A self-hosted deployment: the configured origin is private, and its response URLs (and same-origin redirects) point back at it.
        var fetch = new FetchMock().Once(() => Redirect(trustedOrigin + "/final")).Once(Ok);

        using var response = await Sdk.FetchWithValidatedRedirects.FetchAsync(
            trustedOrigin + "/predictions/123",
            null,
            fetch.Fetch,
            CancellationToken.None,
            trustedOrigin
        );

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(2, fetch.Calls.Count);
    }

    [Theory]
    [InlineData("http://169.254.169.254/")]
    [InlineData("https://other.internal/file")]
    public async Task Validates_hops_on_other_origins_even_when_trustedOrigin_is_set(string redirectUrl)
    {
        var fetch = new FetchMock().Once(() => Redirect(redirectUrl));

        await Assert.ThrowsAsync<DownloadException>(() =>
            Sdk.FetchWithValidatedRedirects.FetchAsync(
                "http://localhost:5000/predictions/123",
                null,
                fetch.Fetch,
                CancellationToken.None,
                "http://localhost:5000"
            )
        );
        Assert.Single(fetch.Calls);
    }

    [Fact]
    public async Task Uses_the_injected_fetch_instead_of_the_global_one()
    {
        var injected = new FetchMock().Once(Ok);

        using var response = await Fetch("https://example.com/file", injected);

        Assert.Single(injected.Calls);
    }

    [Fact]
    public async Task Strips_blocked_request_headers_before_sending()
    {
        var fetch = new FetchMock().Once(Ok);

        using var response = await Fetch(
            "https://example.com/file",
            fetch,
            new Dictionary<string, string>
            {
                ["authorization"] = "Bearer secret",
                ["metadata-flavor"] = "Google",
                ["x-forwarded-for"] = "10.0.0.1",
                ["cookie"] = "session=abc",
            }
        );

        var sent = fetch.Calls[0].Headers;
        Assert.False(sent.ContainsKey("metadata-flavor"));
        Assert.False(sent.ContainsKey("x-forwarded-for"));
        Assert.False(sent.ContainsKey("cookie"));
        Assert.Equal("Bearer secret", sent["authorization"]);
    }

    [Fact]
    public async Task Drops_all_caller_headers_except_user_agent_on_a_cross_origin_redirect_but_keeps_them_same_origin()
    {
        // cross-origin: no caller header, including custom API-key headers the fetch spec would let through, may follow to a different host.
        var crossOrigin = new FetchMock().Once(() => Redirect("https://other.example.net/a")).Once(Ok);

        using (
            await Fetch(
                "https://example.com/file",
                crossOrigin,
                new Dictionary<string, string>
                {
                    ["authorization"] = "Bearer secret",
                    ["x-key"] = "provider-api-key",
                    ["user-agent"] = "ai-sdk/test",
                }
            )
        ) { }

        var secondHop = crossOrigin.Calls[1].Headers;
        Assert.False(secondHop.ContainsKey("authorization"));
        Assert.False(secondHop.ContainsKey("x-key"));
        Assert.Equal("ai-sdk/test", secondHop["user-agent"]);

        // same-origin: caller headers are preserved across the hop.
        var sameOrigin = new FetchMock().Once(() => Redirect("https://example.com/next")).Once(Ok);

        using (
            await Fetch(
                "https://example.com/file",
                sameOrigin,
                new Dictionary<string, string> { ["authorization"] = "Bearer secret", ["x-key"] = "provider-api-key" }
            )
        ) { }

        var sameOriginHop = sameOrigin.Calls[1].Headers;
        Assert.Equal("Bearer secret", sameOriginHop["authorization"]);
        Assert.Equal("provider-api-key", sameOriginHop["x-key"]);
    }

    [Fact]
    public async Task Does_not_re_attach_dropped_headers_when_a_later_hop_returns_to_the_original_origin()
    {
        var fetch = new FetchMock()
            .Once(() => Redirect("https://other.example.net/a"))
            .Once(() => Redirect("https://example.com/back"))
            .Once(Ok);

        using var response = await Fetch(
            "https://example.com/file",
            fetch,
            new Dictionary<string, string> { ["authorization"] = "Bearer secret", ["x-key"] = "provider-api-key" }
        );

        var thirdHop = fetch.Calls[2].Headers;
        Assert.False(thirdHop.ContainsKey("authorization"));
        Assert.False(thirdHop.ContainsKey("x-key"));
    }

    private static Task<HttpResponseMessage> Fetch(
        string url,
        FetchMock fetch,
        IReadOnlyDictionary<string, string>? headers = null
    ) => Sdk.FetchWithValidatedRedirects.FetchAsync(url, headers, fetch.Fetch, CancellationToken.None);

    private static Dictionary<string, string> Lowered(FetchCall call) =>
        call.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value);
}
