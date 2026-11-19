// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>sanitizeRequestHeaders</c> (<c>@ai-sdk/provider-utils</c>).</summary>
internal static class SanitizeRequestHeaders
{
    /// <summary>
    /// Request headers stripped before fetching an untrusted URL: host routing, proxy and origin spoofing, cloud metadata, cookies, and
    /// hop-by-hop transport headers (RFC 7230 section 6.1).
    /// </summary>
    private static readonly HashSet<string> BlockedRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "connection",
        "keep-alive",
        "te",
        "trailer",
        "transfer-encoding",
        "upgrade",
        "host",
        "forwarded",
        "proxy-authorization",
        "via",
        "x-forwarded-for",
        "x-forwarded-host",
        "x-forwarded-proto",
        "x-real-ip",
        "metadata",
        "metadata-flavor",
        "x-aws-ec2-metadata-token",
        "x-metadata-token",
        "cookie",
        "set-cookie",
    };

    /// <summary>A copy of <paramref name="input"/> without the blocked headers. The input is never changed.</summary>
    internal static Dictionary<string, string> Sanitize(IReadOnlyDictionary<string, string> input) =>
        input
            .Where(header => !BlockedRequestHeaders.Contains(header.Key))
            .ToDictionary(header => header.Key.ToLowerInvariant(), header => header.Value, StringComparer.Ordinal);
}
