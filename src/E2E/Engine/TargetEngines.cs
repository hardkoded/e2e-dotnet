// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// Picks the engine of a config target for <c>e2e explore</c> and <c>e2e mcp</c>. There is no upstream counterpart: upstream's config
/// holds a live engine handle, and the port's <c>e2e.config.json</c> names a platform instead. The platform decides: <c>web</c> is a
/// <see cref="WebEngine"/>, and <c>ios</c> and <c>android</c> are built by <c>mobile</c>, which <c>E2E.Mobile</c> supplies.
/// </summary>
internal static class TargetEngines
{
    /// <summary>Builds the engine of <paramref name="target"/>. <paramref name="headless"/> is the web window choice; null leaves it to <c>E2E_HEADLESS</c>.</summary>
    public static IEngine Create(TargetConfig target, bool? headless, Func<TargetConfig, IEngine>? mobile)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Platform switch
        {
            "web" => new WebEngine(headless),
            "ios" or "android" => (mobile ?? throw new ConfigurationException(
                "INVALID_CONFIG",
                $"target \"{target.Name}\" has platform {target.Platform}, but this host has no device engine; use the e2e tool, or set ExploreOptions.Engine.")).Invoke(target),
            _ => throw new ConfigurationException("INVALID_CONFIG", $"target \"{target.Name}\" has platform {target.Platform}; the platform must be web, ios, or android"),
        };
    }
}
