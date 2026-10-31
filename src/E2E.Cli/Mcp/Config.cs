// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Mcp;

/// <summary>Where the project a server serves keeps its config.</summary>
internal static class Config
{
    /// <summary>
    /// The absolute path of a project's config, found without reading it: <paramref name="configPath"/> absolute or relative to
    /// <paramref name="cwd"/>, else the nearest <c>e2e.config.json</c> from <paramref name="cwd"/> upward.
    /// </summary>
    public static string LocateProjectConfig(string cwd, string? configPath)
    {
        if (configPath is not null)
        {
            var absolute = Path.GetFullPath(configPath, cwd);
            return File.Exists(absolute) ? absolute : throw E2EConfig.ExplicitConfigNotFound(cwd, configPath, absolute);
        }

        return E2EConfig.Find(cwd)
            ?? throw new ConfigurationException("CONFIG_NOT_FOUND", "no " + E2EConfig.FileName + " found from " + cwd + " upward; create one, or pass config to open_session");
    }
}
