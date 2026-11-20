// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Vercel.Oidc;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>
/// Vercel runtime lookups. Maps to <c>vercel-environment.ts</c>, which re-exports <c>getVercelOidcToken</c> from
/// <c>@vercel/oidc</c>, ported in the <c>Vercel.Oidc</c> package.
/// </summary>
internal static class VercelEnvironment
{
    private static readonly AsyncLocal<Func<CancellationToken, Task<string>>?> s_oidcTokenOverride = new();

    /// <summary>Test seam that replaces <see cref="GetVercelOidcTokenAsync"/>. Null uses the environment variable.</summary>
    internal static Func<CancellationToken, Task<string>>? OidcTokenOverride
    {
        get => s_oidcTokenOverride.Value;
        set => s_oidcTokenOverride.Value = value;
    }

    /// <summary>
    /// Returns the Vercel OIDC token, refreshing it with the Vercel CLI credentials when it is missing or expired.
    /// Maps to <c>getVercelOidcToken</c>.
    /// </summary>
    public static Task<string> GetVercelOidcTokenAsync(CancellationToken cancellationToken)
    {
        var overrideProvider = OidcTokenOverride;
        if (overrideProvider is not null)
        {
            return overrideProvider(cancellationToken);
        }

        return VercelOidcToken.GetVercelOidcTokenAsync(null, cancellationToken);
    }

    /// <summary>
    /// Returns the <c>x-vercel-id</c> header of the Vercel request context, or null. Maps to <c>getVercelRequestId</c>.
    /// </summary>
    public static string? GetVercelRequestId()
    {
        return VercelRequestContext.GetContextHeader("x-vercel-id");
    }
}
