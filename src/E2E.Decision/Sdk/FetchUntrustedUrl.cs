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
    /// Fetches an untrusted URL with first-hop credential isolation and validated redirects. Without a matching
    /// <paramref name="credentialedOrigin"/> (or <paramref name="trustedOrigin"/> when it is omitted), only allowlisted request metadata is
    /// sent on the first hop; <paramref name="untrustedFirstHopHeaders"/> adds non-credential header names to that list. Proxy, metadata,
    /// cookie, and hop-by-hop headers are sanitized even for a matching origin. Both origins must come from developer configuration, never
    /// from response data.
    /// </summary>
    internal static Task<HttpResponseMessage> FetchAsync(
        string url,
        IReadOnlyDictionary<string, string>? headers,
        FetchFunction? fetch,
        CancellationToken cancellationToken,
        string? credentialedOrigin = null,
        IEnumerable<string>? untrustedFirstHopHeaders = null,
        string? trustedOrigin = null,
        int? maxRedirects = null
    )
    {
        Dictionary<string, string>? firstHopHeaders = null;
        if (headers is not null)
        {
            firstHopHeaders = SanitizeRequestHeaders.Sanitize(headers);
            var origin = credentialedOrigin ?? trustedOrigin;
            if (origin is null || !IsSameOrigin.Check(url, origin))
            {
                var allowedHeaders = new HashSet<string>(
                    SafeUntrustedFirstHopHeaders,
                    StringComparer.OrdinalIgnoreCase
                );
                allowedHeaders.UnionWith(untrustedFirstHopHeaders ?? []);
                firstHopHeaders = firstHopHeaders
                    .Where(header => allowedHeaders.Contains(header.Key))
                    .ToDictionary(header => header.Key, header => header.Value, StringComparer.Ordinal);
            }
        }

        return FetchWithValidatedRedirects.FetchAsync(
            url,
            firstHopHeaders,
            fetch,
            cancellationToken,
            trustedOrigin,
            maxRedirects ?? FetchWithValidatedRedirects.DefaultMaxRedirects
        );
    }
}
