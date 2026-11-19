// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0



namespace E2E.Decision.Sdk.Gateway.VercelOidc;

/// <summary>Maps to <c>VercelOidcTokenError</c> in <c>@vercel/oidc</c> <c>token-error.ts</c>.</summary>
internal sealed class VercelOidcTokenError : Exception
{
    public VercelOidcTokenError(string message, Exception? cause = null)
        : base(message, cause!) { }
}
