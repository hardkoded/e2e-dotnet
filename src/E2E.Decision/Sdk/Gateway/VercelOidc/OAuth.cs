// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk.Gateway.VercelOidc;

/// <summary>A successful OAuth token response.</summary>
internal sealed record TokenSet(string AccessToken, double ExpiresIn, string? RefreshToken);

/// <summary>Maps to <c>oauth.ts</c> in <c>@vercel/oidc</c>: the Vercel CLI refresh-token grant.</summary>
internal static class OAuth
{
    private const string VercelIssuer = "https://vercel.com";
    private const string VercelCliClientId = "cl_HYyOPBNtFMfHhaUn9L4QPfTZz6TP47bp";

    private static readonly string s_userAgent =
        "@vercel/oidc dotnet-"
        + Environment.Version
        + " "
        + RuntimeInformation.OSDescription
        + " ("
        + RuntimeInformation.OSArchitecture
        + ") "
        + Environment.MachineName;

    private static readonly AsyncLocal<HttpMessageHandler?> s_handlerOverride = new();

    private static readonly SemaphoreSlim s_tokenEndpointLock = new(1, 1);
    private static string? s_tokenEndpoint;

    /// <summary>Test seam: the handler every request in this class goes through.</summary>
    internal static HttpMessageHandler? HandlerOverride
    {
        get => s_handlerOverride.Value;
        set => s_handlerOverride.Value = value;
    }

    /// <summary>Test seam: forgets the cached token endpoint.</summary>
    internal static void ResetTokenEndpointCache() => s_tokenEndpoint = null;

    /// <summary>Creates the HTTP client for a request. Callers dispose it.</summary>
    internal static HttpClient CreateHttpClient()
    {
        var handler = HandlerOverride;
        var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    /// <summary>Sends the refresh-token grant to the discovered token endpoint.</summary>
    public static async Task<HttpResponseMessage> RefreshTokenRequestAsync(
        string refreshToken,
        CancellationToken cancellationToken
    )
    {
        var tokenEndpoint = await GetTokenEndpointAsync(cancellationToken).ConfigureAwait(false);
        using var client = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = VercelCliClientId,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                }
            ),
        };
        request.Headers.TryAddWithoutValidation("User-Agent", s_userAgent);
        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a token response. Returns the error or the tokens.</summary>
    public static async Task<(Exception? Error, TokenSet? Tokens)> ProcessTokenResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        using (response)
        {
#if NET
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            var json = JsonNode.Parse(text);
            if (!response.IsSuccessStatusCode)
            {
                var message =
                    json is JsonObject failure && failure["error"] is { } error
                        ? error.ToString()
                        : "Token refresh failed";
                return (new InvalidOperationException(message), null);
            }

            if (json is not JsonObject body)
            {
                return (new InvalidOperationException("Invalid token response"), null);
            }

            if (body["access_token"] is not JsonValue accessToken || !accessToken.TryGetValue<string>(out var access))
            {
                return (new InvalidOperationException("Missing access_token in response"), null);
            }

            if (
                body["token_type"] is not JsonValue tokenType
                || !tokenType.TryGetValue<string>(out var type)
                || type != "Bearer"
            )
            {
                return (new InvalidOperationException("Invalid token_type in response"), null);
            }

            if (body["expires_in"] is not JsonValue expiresIn || !expiresIn.TryGetValue<double>(out var expires))
            {
                return (new InvalidOperationException("Missing expires_in in response"), null);
            }

            var refresh = body["refresh_token"] is JsonValue r && r.TryGetValue<string>(out var rt) ? rt : null;
            return (null, new TokenSet(access, expires, refresh));
        }
    }

    private static async Task<string> GetTokenEndpointAsync(CancellationToken cancellationToken)
    {
        if (s_tokenEndpoint is { } cached)
        {
            return cached;
        }

        await s_tokenEndpointLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (s_tokenEndpoint is { } again)
            {
                return again;
            }

            using var client = CreateHttpClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                VercelIssuer + "/.well-known/openid-configuration"
            );
            request.Headers.TryAddWithoutValidation("User-Agent", s_userAgent);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("Failed to discover OAuth endpoints");
            }

#if NET
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            if (
                JsonNode.Parse(text) is not JsonObject metadata
                || metadata["token_endpoint"] is not JsonValue endpoint
                || !endpoint.TryGetValue<string>(out var tokenEndpoint)
            )
            {
                throw new InvalidOperationException("Invalid OAuth discovery response");
            }

            if (
                !Uri.TryCreate(tokenEndpoint, UriKind.Absolute, out var endpointUri)
                || endpointUri.Scheme != Uri.UriSchemeHttps
            )
            {
                throw new InvalidOperationException("Invalid OAuth discovery response");
            }

            s_tokenEndpoint = tokenEndpoint;
            return tokenEndpoint;
        }
        finally
        {
            s_tokenEndpointLock.Release();
        }
    }
}
