// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.FetchMock;

namespace E2E.Decision.Tests.FetchUntrustedUrl;

/// <summary>The AI SDK's <c>fetch-untrusted-url.test.ts</c>, <c>describe('fetchUntrustedUrl')</c>.</summary>
public sealed class FetchUntrustedUrlTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("https://provider.example.com")]
    [InlineData("http://example.com")]
    [InlineData("https://example.com:8443")]
    public async Task Withholds_credentials_and_unknown_headers_without_a_matching_credentialedOrigin(
        string? credentialedOrigin
    )
    {
        var fetch = new FetchMock().Once(Ok);

        await Sdk.FetchUntrustedUrl.FetchAsync(
            "https://example.com/file",
            new Dictionary<string, string>
            {
                ["authorization"] = "Bearer secret",
                ["x-key"] = "provider-secret",
                ["x-jfrog-art-api"] = "vendor-secret",
                ["x-access-token"] = "access-token",
                ["x-client-secret"] = "client-secret",
                ["x-goog-api-key"] = "google-api-key",
                ["custom-vendor-header"] = "another-secret",
                ["accept"] = "image/*",
                ["range"] = "bytes=0-1023",
                ["idempotency-key"] = "operation-id",
                ["x-request-id"] = "request-id",
                ["user-agent"] = "ai-sdk/test",
            },
            fetch.Fetch,
            CancellationToken.None,
            credentialedOrigin: credentialedOrigin
        );

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["accept"] = "image/*",
                ["range"] = "bytes=0-1023",
                ["idempotency-key"] = "operation-id",
                ["x-request-id"] = "request-id",
                ["user-agent"] = "ai-sdk/test",
            },
            fetch.Calls[0].Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value)
        );
    }

    [Theory]
    [InlineData("https://example.com/api", null)]
    [InlineData(null, "https://example.com")]
    public async Task Preserves_sanitized_headers_for_a_matching_origin(
        string? credentialedOrigin,
        string? trustedOrigin
    )
    {
        var fetch = new FetchMock().Once(Ok);
        var headers = new Dictionary<string, string>
        {
            ["authorization"] = "Bearer secret",
            ["x-jfrog-art-api"] = "vendor-secret",
            ["x-custom-metadata"] = "custom-value",
            ["cookie"] = "session=abc",
            ["metadata-flavor"] = "Google",
            ["x-forwarded-for"] = "10.0.0.1",
        };

        await Sdk.FetchUntrustedUrl.FetchAsync(
            "https://example.com/file",
            headers,
            fetch.Fetch,
            CancellationToken.None,
            credentialedOrigin: credentialedOrigin,
            trustedOrigin: trustedOrigin
        );

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["authorization"] = "Bearer secret",
                ["x-jfrog-art-api"] = "vendor-secret",
                ["x-custom-metadata"] = "custom-value",
            },
            fetch.Calls[0].Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value)
        );
        Assert.Equal("session=abc", headers["cookie"]);
        Assert.Equal("Google", headers["metadata-flavor"]);
    }

    [Fact]
    public async Task Withholds_credentials_when_only_a_non_matching_trustedOrigin_is_provided()
    {
        var fetch = new FetchMock().Once(Ok);

        await Sdk.FetchUntrustedUrl.FetchAsync(
            "https://cdn.example.com/file",
            new Dictionary<string, string> { ["authorization"] = "Bearer secret", ["x-key"] = "provider-secret" },
            fetch.Fetch,
            CancellationToken.None,
            trustedOrigin: "https://api.example.com"
        );

        Assert.Empty(fetch.Calls[0].Headers);
    }

    [Fact]
    public async Task Uses_credentialedOrigin_to_narrow_a_matching_trustedOrigin()
    {
        var fetch = new FetchMock().Once(Ok);

        await Sdk.FetchUntrustedUrl.FetchAsync(
            "http://localhost:5000/predictions/123",
            new Dictionary<string, string> { ["authorization"] = "Bearer secret", ["accept"] = "application/json" },
            fetch.Fetch,
            CancellationToken.None,
            credentialedOrigin: "https://api.example.com",
            trustedOrigin: "http://localhost:5000"
        );

        Assert.Equal(new Dictionary<string, string> { ["accept"] = "application/json" }, Lowered(fetch.Calls[0]));
    }

    [Fact]
    public async Task Preserves_caller_approved_metadata_but_cannot_reintroduce_sanitized_headers()
    {
        var fetch = new FetchMock().Once(Ok);

        await Sdk.FetchUntrustedUrl.FetchAsync(
            "https://example.com/file",
            new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer secret",
                ["Proxy-Authorization"] = "Basic secret",
                ["X-Protocol-Version"] = "2026-09-24",
            },
            fetch.Fetch,
            CancellationToken.None,
            untrustedFirstHopHeaders: ["X-Protocol-Version", "Proxy-Authorization"]
        );

        Assert.Equal(new Dictionary<string, string> { ["x-protocol-version"] = "2026-09-24" }, Lowered(fetch.Calls[0]));
    }

    [Fact]
    public async Task Preserves_headers_on_same_origin_redirects_drops_them_across_origins_and_never_reattaches_them()
    {
        var fetch = new FetchMock()
            .Once(() => Redirect("https://example.com/next"))
            .Once(() => Redirect("https://cdn.example.net/file"))
            .Once(() => Redirect("https://example.com/back"))
            .Once(Ok);
        var headers = new Dictionary<string, string>
        {
            ["authorization"] = "Bearer secret",
            ["x-key"] = "provider-secret",
            ["x-protocol-version"] = "2026-09-24",
            ["user-agent"] = "ai-sdk/test",
        };

        await Sdk.FetchUntrustedUrl.FetchAsync(
            "https://example.com/file",
            headers,
            fetch.Fetch,
            CancellationToken.None,
            credentialedOrigin: "https://example.com",
            untrustedFirstHopHeaders: ["x-protocol-version"]
        );

        var userAgentOnly = new Dictionary<string, string> { ["user-agent"] = "ai-sdk/test" };
        Assert.Equal(new[] { headers, headers, userAgentOnly, userAgentOnly }, fetch.Calls.Select(Lowered));
    }

    [Fact]
    public async Task Never_attaches_withheld_credentials_when_an_untrusted_first_hop_redirects_to_the_credentialed_origin()
    {
        var fetch = new FetchMock().Once(() => Redirect("https://api.example.com/file")).Once(Ok);

        await Sdk.FetchUntrustedUrl.FetchAsync(
            "https://untrusted.example/file",
            new Dictionary<string, string> { ["authorization"] = "Bearer secret", ["x-key"] = "provider-secret" },
            fetch.Fetch,
            CancellationToken.None,
            credentialedOrigin: "https://api.example.com"
        );

        Assert.Equal(2, fetch.Calls.Count);
        foreach (var call in fetch.Calls)
        {
            Assert.Empty(call.Headers);
        }
    }

    [Fact]
    public async Task Rejects_private_URLs_even_when_credentialedOrigin_matches()
    {
        var fetch = new FetchMock();

        await Assert.ThrowsAsync<DownloadException>(() =>
            Sdk.FetchUntrustedUrl.FetchAsync(
                "http://127.0.0.1/file",
                new Dictionary<string, string> { ["authorization"] = "Bearer secret" },
                fetch.Fetch,
                CancellationToken.None,
                credentialedOrigin: "http://127.0.0.1"
            )
        );
        Assert.Empty(fetch.Calls);
    }

    [Fact]
    public async Task Allows_a_configured_private_origin_but_rejects_a_redirect_to_another_private_origin()
    {
        var fetch = new FetchMock().Once(() => Redirect("http://10.0.0.1/file"));

        await Assert.ThrowsAsync<DownloadException>(() =>
            Sdk.FetchUntrustedUrl.FetchAsync(
                "http://localhost:5000/file",
                new Dictionary<string, string> { ["authorization"] = "Bearer secret" },
                fetch.Fetch,
                CancellationToken.None,
                trustedOrigin: "http://localhost:5000"
            )
        );

        Assert.Single(fetch.Calls);
        Assert.Equal("Bearer secret", fetch.Calls[0].Headers["authorization"]);
    }

    [Fact]
    public async Task Passes_abort_signals_and_redirect_limits_through_without_adding_headers_to_a_bare_request()
    {
        var fetch = new FetchMock().Once(() => Redirect("https://example.com/next"));
        using var cts = new CancellationTokenSource();

        var error = await Assert.ThrowsAsync<DownloadException>(() =>
            Sdk.FetchUntrustedUrl.FetchAsync("https://example.com/file", null, fetch.Fetch, cts.Token, maxRedirects: 0)
        );

        Assert.Contains("Too many redirects (max 0)", error.Message);
        var call = Assert.Single(fetch.Calls);
        Assert.Equal("https://example.com/file", call.Url);
        Assert.Equal(cts.Token, call.Token);
        Assert.Empty(call.Headers);
    }

    private static Dictionary<string, string> Lowered(FetchCall call) =>
        call.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value);
}
