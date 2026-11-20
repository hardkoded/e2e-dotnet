// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>isSameOrigin</c> (<c>@ai-sdk/provider-utils</c>).</summary>
internal static class IsSameOrigin
{
    /// <summary>Whether <paramref name="url"/> has the same origin (scheme, host, and port) as <paramref name="baseUrl"/>. False when either is not an absolute URL.</summary>
    internal static bool Check(string url, string baseUrl) =>
        Uri.TryCreate(url, UriKind.Absolute, out var left)
        && Uri.TryCreate(baseUrl, UriKind.Absolute, out var right)
        && left.Scheme == right.Scheme
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;
}
