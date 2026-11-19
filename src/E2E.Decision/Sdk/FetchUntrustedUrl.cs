// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>fetchUntrustedUrl</c> (<c>@ai-sdk/provider-utils</c>).</summary>
internal static class FetchUntrustedUrl
{
    // Providers can use arbitrary credential header names. Only established non-credential request metadata is safe to send to an untrusted URL.
    private static readonly HashSet<string> SafeUntrustedFirstHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "accept",
        "accept-language",
        "baggage",
        "cache-control",
        "idempotency-key",
        "if-match",
        "if-modified-since",
        "if-none-match",
        "if-range",
        "if-unmodified-since",
        "pragma",
        "range",
        "traceparent",
        "tracestate",
        "user-agent",
        "x-correlation-id",
        "x-request-id",
    };

    /// <summary>
    /// Fetches an untrusted URL with first-hop credential isolation and validated redirects: only allowlisted request metadata is sent on the
    /// first hop. Upstream's <c>credentialedOrigin</c>, <c>trustedOrigin</c>, and <c>untrustedFirstHopHeaders</c> are not ported, since
    /// nothing in the port passes them.
    /// </summary>
    internal static Task<HttpResponseMessage> FetchAsync(
        string url,
        IReadOnlyDictionary<string, string>? headers,
        FetchFunction? fetch,
        CancellationToken cancellationToken
    ) =>
        FetchWithValidatedRedirects.FetchAsync(
            url,
            headers
                ?.Where(header => SafeUntrustedFirstHopHeaders.Contains(header.Key))
                .ToDictionary(header => header.Key, header => header.Value, StringComparer.OrdinalIgnoreCase),
            fetch,
            cancellationToken
        );
}
