// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0



namespace E2E.Decision.Sdk.Gateway.VercelOidc;

/// <summary>Maps to <c>AccessTokenMissingError</c> in <c>@vercel/oidc</c> <c>auth-errors.ts</c>.</summary>
internal sealed class AccessTokenMissingError : Exception
{
    public AccessTokenMissingError()
        : base("No authentication found. Please log in with the Vercel CLI (vercel login).") { }
}

/// <summary>Maps to <c>RefreshAccessTokenFailedError</c> in <c>@vercel/oidc</c> <c>auth-errors.ts</c>.</summary>
internal sealed class RefreshAccessTokenFailedError : Exception
{
    public RefreshAccessTokenFailedError(Exception? cause)
        : base("Failed to refresh authentication token.", cause!) { }

    public RefreshAccessTokenFailedError(string cause)
        : this(new InvalidOperationException(cause)) { }
}
