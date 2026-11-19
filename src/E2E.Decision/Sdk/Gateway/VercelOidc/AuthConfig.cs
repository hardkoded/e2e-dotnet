// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk.Gateway.VercelOidc;

/// <summary>The Vercel CLI <c>auth.json</c> contents.</summary>
internal sealed class AuthConfig
{
    public string? Token { get; set; }

    public double? ExpiresAt { get; set; }

    public string? RefreshToken { get; set; }
}

/// <summary>Maps to <c>auth-config.ts</c> in <c>@vercel/oidc</c>.</summary>
internal static class AuthConfigStore
{
    /// <summary>Reads the CLI auth file. Returns null when it is missing, empty or unreadable.</summary>
    public static AuthConfig? ReadAuthConfig()
    {
        try
        {
            var authPath = GetAuthConfigPath();
            if (!File.Exists(authPath))
            {
                return null;
            }

            var content = File.ReadAllText(authPath);
            if (content.Length == 0)
            {
                return null;
            }

            if (JsonNode.Parse(content) is not JsonObject json)
            {
                return null;
            }

            return new AuthConfig
            {
                Token = json["token"] is JsonValue token && token.TryGetValue<string>(out var t) ? t : null,
                ExpiresAt = json["expiresAt"] is JsonValue exp && exp.TryGetValue<double>(out var e) ? e : null,
                RefreshToken =
                    json["refreshToken"] is JsonValue refresh && refresh.TryGetValue<string>(out var r) ? r : null,
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Writes the CLI auth file with owner-only permissions.</summary>
    public static void WriteAuthConfig(AuthConfig config)
    {
        var authPath = GetAuthConfigPath();
        var authDir = Path.GetDirectoryName(authPath)!;
        FileModes.CreateRestrictedDirectory(authDir);

        var json = new JsonObject();
        if (config.Token is not null)
        {
            json["token"] = config.Token;
        }

        if (config.ExpiresAt is not null)
        {
            json["expiresAt"] = config.ExpiresAt;
        }

        if (config.RefreshToken is not null)
        {
            json["refreshToken"] = config.RefreshToken;
        }

        FileModes.WriteRestricted(authPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>True when the token is present and not expired within the buffer. A token without an expiry is valid.</summary>
    public static bool IsValidAccessToken(AuthConfig authConfig, double expirationBufferMs = 0)
    {
        if (string.IsNullOrEmpty(authConfig.Token))
        {
            return false;
        }

        if (authConfig.ExpiresAt is not { } expiresAt)
        {
            return true;
        }

        var nowInSeconds = Math.Floor((double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);
        var bufferInSeconds = expirationBufferMs / 1000;
        return expiresAt >= nowInSeconds + bufferInSeconds;
    }

    private static string GetAuthConfigPath()
    {
        var dataDir = TokenUtil.GetVercelDataDir()
            ?? throw new InvalidOperationException(
                "Unable to find Vercel CLI data directory. Your platform: "
                    + System.Runtime.InteropServices.RuntimeInformation.OSDescription
                    + ". Supported: darwin, linux, win32."
            );
        return Path.Combine(dataDir, "auth.json");
    }
}
