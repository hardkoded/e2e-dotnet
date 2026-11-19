// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace E2E.Decision.Sdk.Gateway.VercelOidc;

/// <summary>Maps to <c>token-io.ts</c> in <c>@vercel/oidc</c>: where the Vercel CLI keeps its files.</summary>
internal static class TokenIo
{
    private static readonly AsyncLocal<Func<string?>?> s_findRootDirOverride = new();
    private static readonly AsyncLocal<Func<string?>?> s_getUserDataDirOverride = new();

    /// <summary>Test seam that replaces <see cref="FindRootDir"/>.</summary>
    internal static Func<string?>? FindRootDirOverride
    {
        get => s_findRootDirOverride.Value;
        set => s_findRootDirOverride.Value = value;
    }

    /// <summary>Test seam that replaces <see cref="GetUserDataDir"/>.</summary>
    internal static Func<string?>? GetUserDataDirOverride
    {
        get => s_getUserDataDirOverride.Value;
        set => s_getUserDataDirOverride.Value = value;
    }

    /// <summary>Walks up from the working directory to the first directory that has a <c>.vercel</c> entry.</summary>
    public static string? FindRootDir()
    {
        var overrideProvider = FindRootDirOverride;
        if (overrideProvider is not null)
        {
            return overrideProvider();
        }

        try
        {
            var dir = Directory.GetCurrentDirectory();
            while (true)
            {
                var parent = Path.GetDirectoryName(dir);
                if (parent is null || parent == dir)
                {
                    break;
                }

                var path = Path.Combine(dir, ".vercel");
                if (File.Exists(path) || Directory.Exists(path))
                {
                    return dir;
                }

                dir = parent;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new VercelOidcTokenError("Token refresh only supported in node server environments", exception);
        }

        return null;
    }

    /// <summary>Returns the per-user data directory, or null on an unsupported platform.</summary>
    public static string? GetUserDataDir()
    {
        var overrideProvider = GetUserDataDirOverride;
        if (overrideProvider is not null)
        {
            return overrideProvider();
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrEmpty(xdgDataHome))
        {
            return xdgDataHome;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(home, "Library", "Application Support");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Path.Combine(home, ".local", "share");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            return string.IsNullOrEmpty(localAppData) ? null : localAppData;
        }

        return null;
    }
}
