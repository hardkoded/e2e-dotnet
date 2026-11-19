// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.Gateway.VercelOidc;

/// <summary>Options for <see cref="VercelOidcToken.GetVercelOidcTokenAsync"/>. Maps to the <c>options</c> of <c>getVercelOidcToken</c>.</summary>
internal sealed class VercelOidcTokenOptions
{
    /// <summary>Treat the token as expired this many milliseconds before it is.</summary>
    public double? ExpirationBufferMs { get; set; }

    /// <summary>Project id. Read from <c>.vercel/project.json</c> when unset.</summary>
    public string? Project { get; set; }

    /// <summary>Team id. Read from <c>.vercel/project.json</c> when unset.</summary>
    public string? Team { get; set; }
}

/// <summary>Maps to <c>get-vercel-oidc-token.ts</c> in <c>@vercel/oidc</c>.</summary>
internal static class VercelOidcToken
{
    /// <summary>Returns the OIDC token, refreshing it through the Vercel CLI credentials when it is missing or expired.</summary>
    public static async Task<string> GetVercelOidcTokenAsync(
        VercelOidcTokenOptions? options,
        CancellationToken cancellationToken
    )
    {
        var token = "";
        Exception? err = null;
        try
        {
            token = GetVercelOidcTokenSync();
        }
        catch (InvalidOperationException error)
        {
            err = error;
        }

        try
        {
            if (
                token.Length == 0
                || TokenUtil.IsExpired(
                    TokenUtil.GetTokenPayload(token),
                    options?.ExpirationBufferMs ?? 0
                )
            )
            {
                await Token.RefreshTokenAsync(options, cancellationToken).ConfigureAwait(false);
                token = GetVercelOidcTokenSync();
            }
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            throw new VercelOidcTokenError(
                (err is null ? "" : err.Message + "\n") + error.Message,
                error
            );
        }

        return token;
    }

    /// <summary>
    /// Reads the OIDC token from the <c>x-vercel-oidc-token</c> header of the Vercel request context
    /// (<see cref="VercelRequestContext"/>), then from <c>VERCEL_OIDC_TOKEN</c> when the header is absent (upstream <c>??</c>).
    /// The refresh in <see cref="GetVercelOidcTokenAsync"/> only updates <c>VERCEL_OIDC_TOKEN</c>, so an expired request
    /// header token is still the one found afterwards, as upstream; hosts must supply a fresh request token.
    /// </summary>
    public static string GetVercelOidcTokenSync()
    {
        var token = VercelEnvironment.GetContextHeader("x-vercel-oidc-token");
        if (token is null)
        {
            token = Environment.GetEnvironmentVariable(
                VercelEnvironment.OidcTokenEnvironmentVariable
            );
        }

        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException(
                "The 'x-vercel-oidc-token' header is missing from the request. Do you have the OIDC option enabled in the Vercel project settings?"
            );
        }

        return token!;
    }
}
