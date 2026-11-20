// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.FetchMock;

namespace E2E.Decision.Tests.FetchWithValidatedRedirects;

/// <summary>The AI SDK's <c>fetch-with-validated-redirects.test.ts</c>, <c>describe('fetchWithValidatedEndpoint')</c>.</summary>
public sealed class FetchWithValidatedEndpointTests
{
    private const string DnsValidationMessage =
        "This server runtime requires a custom fetch with connect-time DNS validation for untrusted URLs";

    [Fact]
    public async Task Partial_Uses_the_selected_runtime_transport_custom()
    {
        // Upstream also runs the "default" and "forwarded global" modes against a stubbed globalThis.fetch. The port has no global fetch
        // to stub, so only the custom transport is checked.
        var runtimeFetch = new FetchMock().Always(Ok);
        var customFetch = new FetchMock().Always(Ok);

        using var response = await Sdk.FetchWithValidatedRedirects.FetchEndpointAsync(
            "https://files.example.com/file",
            null,
            customFetch.Fetch,
            null,
            "error",
            false,
            CancellationToken.None
        );

        var call = Assert.Single(customFetch.Calls);
        Assert.Equal("https://files.example.com/file", call.Url);
        Assert.Equal("error", call.Redirect);
        Assert.Empty(runtimeFetch.Calls);
    }

    [Fact]
    public async Task Validates_the_URL_before_issuing_the_request()
    {
        var fetch = new FetchMock();

        await Assert.ThrowsAsync<DownloadException>(() =>
            Sdk.FetchWithValidatedRedirects.FetchEndpointAsync(
                "http://169.254.169.254/token",
                null,
                fetch.Fetch,
                null,
                "error",
                false,
                CancellationToken.None
            )
        );
        Assert.Empty(fetch.Calls);
    }

    [Fact]
    public async Task Partial_Rejects_the_runtime_fetch_with_strict_DNS_validation_enabled_default()
    {
        // Upstream also runs the "forwarded global" mode, which has no counterpart without a global fetch.
        var error = await Assert.ThrowsAsync<DownloadException>(() =>
            Sdk.FetchWithValidatedRedirects.FetchEndpointAsync(
                "https://example.com/metadata",
                null,
                null,
                null,
                "error",
                true,
                CancellationToken.None
            )
        );

        Assert.Contains(DnsValidationMessage, error.Message);
    }

    [Fact]
    public async Task Uses_a_custom_fetch_with_strict_DNS_validation_enabled()
    {
        var customFetch = new FetchMock().Always(Ok);

        using var response = await Sdk.FetchWithValidatedRedirects.FetchEndpointAsync(
            "https://example.com/metadata",
            null,
            customFetch.Fetch,
            null,
            "error",
            true,
            CancellationToken.None
        );

        Assert.Single(customFetch.Calls);
    }

    [Fact]
    public async Task Partial_Allows_the_trusted_origin_through_the_runtime_fetch_with_strict_DNS_validation_enabled()
    {
        // The port cannot stub its default transport, so this reaches it for real: nothing listens on the port, and what matters is that
        // the strict DNS validation does not reject the trusted origin before the request.
        var error = await Record.ExceptionAsync(() =>
            Sdk.FetchWithValidatedRedirects.FetchEndpointAsync(
                "http://localhost:3000/metadata",
                null,
                null,
                "http://localhost:3000",
                "error",
                true,
                CancellationToken.None
            )
        );

        Assert.IsNotType<DownloadException>(error);
    }

    [Fact]
    public async Task Uses_redirect_error_for_credential_bearing_requests()
    {
        var fetch = new FetchMock().Once(Ok);
        using var init = new HttpRequestMessage(HttpMethod.Post, "https://auth.example.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "secret-code" }),
        };
        init.Headers.TryAddWithoutValidation("authorization", "Basic secret");

        using var response = await Sdk.FetchWithValidatedRedirects.FetchEndpointAsync(
            "https://auth.example.com/token",
            init,
            fetch.Fetch,
            null,
            "error",
            false,
            CancellationToken.None
        );

        var call = Assert.Single(fetch.Calls);
        Assert.Equal("https://auth.example.com/token", call.Url);
        Assert.Equal("Basic secret", call.Headers["authorization"]);
        Assert.Equal("error", call.Redirect);
        Assert.Equal(HttpMethod.Post, init.Method);
        Assert.Equal("code=secret-code", await init.Content!.ReadAsStringAsync());
    }
}
