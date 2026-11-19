// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk.Gateway.VercelOidc;

/// <summary>The claims of an OIDC token that <c>@vercel/oidc</c> reads.</summary>
internal sealed record TokenPayload(string? Sub, string? Name, double Exp);

/// <summary>The body the Vercel API returns for an OIDC token refresh. Keeps the whole object so it is cached as received.</summary>
internal sealed class VercelTokenResponse
{
    public VercelTokenResponse(string token, JsonObject raw)
    {
        Token = token;
        Raw = raw;
    }

    public string Token { get; }

    public JsonObject Raw { get; }
}

/// <summary>Project ids read from <c>.vercel/project.json</c>.</summary>
internal sealed record ProjectInfo(string? ProjectId, string? TeamId);

/// <summary>Maps to <c>token-util.ts</c> in <c>@vercel/oidc</c>.</summary>
internal static class TokenUtil
{
    /// <summary>The Vercel CLI data directory, or null when the user data directory is unknown.</summary>
    public static string? GetVercelDataDir()
    {
        var dataDir = TokenIo.GetUserDataDir();
        return dataDir is null ? null : Path.Combine(dataDir, "com.vercel.cli");
    }

    /// <summary>Returns a valid Vercel CLI access token, refreshing it when it has expired.</summary>
    public static async Task<string> GetVercelTokenAsync(
        VercelOidcTokenOptions? options,
        CancellationToken cancellationToken
    )
    {
        var authConfig = AuthConfigStore.ReadAuthConfig();
        if (string.IsNullOrEmpty(authConfig?.Token))
        {
            throw new AccessTokenMissingError();
        }

        if (AuthConfigStore.IsValidAccessToken(authConfig!, options?.ExpirationBufferMs ?? 0))
        {
            return authConfig!.Token!;
        }

        if (string.IsNullOrEmpty(authConfig!.RefreshToken))
        {
            AuthConfigStore.WriteAuthConfig(new AuthConfig());
            throw new RefreshAccessTokenFailedError("No refresh token available");
        }

        try
        {
            var tokenResponse = await OAuth
                .RefreshTokenRequestAsync(authConfig.RefreshToken!, cancellationToken)
                .ConfigureAwait(false);
            var (tokensError, tokens) = await OAuth
                .ProcessTokenResponseAsync(tokenResponse, cancellationToken)
                .ConfigureAwait(false);
            if (tokensError is not null || tokens is null)
            {
                AuthConfigStore.WriteAuthConfig(new AuthConfig());
                throw new RefreshAccessTokenFailedError(tokensError);
            }

            var updatedConfig = new AuthConfig
            {
                Token = tokens.AccessToken,
                ExpiresAt =
                    Math.Floor((double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) + tokens.ExpiresIn,
            };
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
            {
                updatedConfig.RefreshToken = tokens.RefreshToken;
            }

            AuthConfigStore.WriteAuthConfig(updatedConfig);
            return updatedConfig.Token;
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            AuthConfigStore.WriteAuthConfig(new AuthConfig());
            if (error is AccessTokenMissingError or RefreshAccessTokenFailedError)
            {
                throw;
            }

            throw new RefreshAccessTokenFailedError(error);
        }
    }

    /// <summary>Asks the Vercel API for a fresh OIDC token for the project.</summary>
    public static async Task<VercelTokenResponse> GetVercelOidcTokenAsync(
        string authToken,
        string projectId,
        string? teamId,
        CancellationToken cancellationToken
    )
    {
        var url =
            "https://api.vercel.com/v1/projects/"
            + Uri.EscapeDataString(projectId)
            + "/token?source=vercel-oidc-refresh"
            + (string.IsNullOrEmpty(teamId) ? "" : "&teamId=" + Uri.EscapeDataString(teamId!));
        using var client = OAuth.CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + authToken);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new VercelOidcTokenError("Failed to refresh OIDC token: " + response.ReasonPhrase);
        }

#if NET
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        return AssertVercelOidcTokenResponse(JsonNode.Parse(text));
    }

    /// <summary>Checks the shape of an OIDC token response.</summary>
    public static VercelTokenResponse AssertVercelOidcTokenResponse(JsonNode? response)
    {
        if (response is not JsonObject obj)
        {
            throw new InvalidDataException(
                "Vercel OIDC token is malformed. Expected an object. Please run `vc env pull` and try again"
            );
        }

        if (obj["token"] is not JsonValue value || !value.TryGetValue<string>(out var token))
        {
            throw new InvalidDataException(
                "Vercel OIDC token is malformed. Expected a string-valued token property. Please run `vc env pull` and try again"
            );
        }

        return new VercelTokenResponse(token, obj);
    }

    /// <summary>Reads <c>.vercel/project.json</c> from the project root.</summary>
    public static ProjectInfo FindProjectInfo()
    {
        var dir = TokenIo.FindRootDir()
            ?? throw new VercelOidcTokenError(
                "Unable to find project root directory. Have you linked your project with `vc link?`"
            );
        var prjPath = Path.Combine(dir, ".vercel", "project.json");
        if (!File.Exists(prjPath))
        {
            throw new VercelOidcTokenError("project.json not found, have you linked your project with `vc link?`");
        }

        var prj = JsonNode.Parse(File.ReadAllText(prjPath)) as JsonObject;
        var projectId = StringProperty(prj, "projectId");
        var orgId = StringProperty(prj, "orgId");
        if (projectId is null && orgId is null)
        {
            throw new InvalidDataException(
                "Expected a string-valued projectId property. Try running `vc link` to re-link your project."
            );
        }

        return new ProjectInfo(projectId, orgId);
    }

    /// <summary>Caches the OIDC token response for the project.</summary>
    public static void SaveToken(VercelTokenResponse token, string projectId)
    {
        var dir = TokenIo.GetUserDataDir()
            ?? throw new VercelOidcTokenError(
                "Unable to find user data directory. Please reach out to Vercel support."
            );
        var tokenPath = Path.Combine(dir, "com.vercel.token", projectId + ".json");
        var tokenDir = Path.GetDirectoryName(tokenPath)!;
        FileModes.CreateRestrictedDirectory(tokenDir);

        FileModes.WriteRestricted(tokenPath, token.Raw.ToJsonString(), groupAccess: true);
    }

    /// <summary>Loads the cached OIDC token response for the project, or null when there is none.</summary>
    public static VercelTokenResponse? LoadToken(string projectId)
    {
        var dir = TokenIo.GetUserDataDir()
            ?? throw new VercelOidcTokenError(
                "Unable to find user data directory. Please reach out to Vercel support."
            );
        var tokenPath = Path.Combine(dir, "com.vercel.token", projectId + ".json");
        if (!File.Exists(tokenPath))
        {
            return null;
        }

        return AssertVercelOidcTokenResponse(JsonNode.Parse(File.ReadAllText(tokenPath)));
    }

    /// <summary>Decodes the payload segment of a JWT.</summary>
    public static TokenPayload GetTokenPayload(string token)
    {
        var tokenParts = token.Split('.');
        if (tokenParts.Length != 3)
        {
            throw new VercelOidcTokenError("Invalid token. Please run `vc env pull` and try again");
        }

        var base64 = tokenParts[1].Replace('-', '+').Replace('_', '/');
        var padded = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        var json = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
        if (json is not JsonObject obj)
        {
            throw new InvalidDataException("Invalid token payload.");
        }

        return new TokenPayload(
            StringProperty(obj, "sub"),
            StringProperty(obj, "name"),
            obj["exp"] is JsonValue exp && exp.TryGetValue<double>(out var e) ? e : double.NaN
        );
    }

    /// <summary>True when the token expires within the buffer. A token without an expiry never counts as expired, as in JS where <c>NaN</c> compares false.</summary>
    public static bool IsExpired(TokenPayload token, double bufferMs = 0) =>
        token.Exp * 1000 < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + bufferMs;

    private static string? StringProperty(JsonObject? obj, string name) =>
        obj?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
