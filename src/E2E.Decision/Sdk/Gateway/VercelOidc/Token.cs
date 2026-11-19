// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.Gateway.VercelOidc;

/// <summary>Maps to <c>token.ts</c> in <c>@vercel/oidc</c>.</summary>
internal static class Token
{
    // .NET-only: concurrent refreshes would each spend the same refresh token, and a rejected one clears auth.json.
    private static readonly SemaphoreSlim s_refreshLock = new(1, 1);

    /// <summary>Refreshes the OIDC token from the cache or the Vercel API and publishes it in <c>VERCEL_OIDC_TOKEN</c>.</summary>
    public static async Task RefreshTokenAsync(VercelOidcTokenOptions? options, CancellationToken cancellationToken)
    {
        var projectId = options?.Project;
        var teamId = options?.Team;
        if (projectId is null && teamId is null)
        {
            var projectInfo = TokenUtil.FindProjectInfo();
            projectId = projectInfo.ProjectId;
            teamId = projectInfo.TeamId;
        }
        else if (projectId is null || teamId is null)
        {
            var projectInfo = TokenUtil.FindProjectInfo();
            projectId ??= projectInfo.ProjectId;
            teamId ??= projectInfo.TeamId;
        }

        if (string.IsNullOrEmpty(projectId))
        {
            throw new VercelOidcTokenError(
                "Failed to refresh OIDC token: No project specified. Try re-linking your project with `vc link`"
            );
        }

        await s_refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var maybeToken = TokenUtil.LoadToken(projectId!);
            if (
                maybeToken is null
                || TokenUtil.IsExpired(TokenUtil.GetTokenPayload(maybeToken.Token), options?.ExpirationBufferMs ?? 0)
            )
            {
                var authToken = await TokenUtil.GetVercelTokenAsync(options, cancellationToken).ConfigureAwait(false);
                maybeToken = await TokenUtil
                    .GetVercelOidcTokenAsync(authToken, projectId!, teamId, cancellationToken)
                    .ConfigureAwait(false);
                TokenUtil.SaveToken(maybeToken, projectId!);
            }

            Environment.SetEnvironmentVariable(VercelEnvironment.OidcTokenEnvironmentVariable, maybeToken.Token);
        }
        finally
        {
            s_refreshLock.Release();
        }
    }
}
