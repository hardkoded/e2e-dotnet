// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>The target a session opens: named, or the only one the config declares. Upstream's <c>run/select-target.ts</c>.</summary>
internal static class SelectTarget
{
    /// <summary>
    /// <paramref name="name"/> must be a declared target; without one, the config must declare exactly one. <paramref name="howToName"/> tells
    /// the caller how to name it when several exist. Upstream's <c>selectTarget</c>.
    /// </summary>
    public static TargetConfig Select(IReadOnlyList<TargetConfig> targets, string? name, string howToName)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var names = string.Join(", ", targets.Select(target => "\"" + target.Name + "\""));
        if (name is not null)
        {
            return targets.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal))
                ?? throw new ConfigurationException("UNKNOWN_TARGET", "unknown target \"" + name + "\"; the config declares " + names);
        }

        return targets.Count switch
        {
            0 => throw new ConfigurationException("INVALID_CONFIG", "the config declares no targets"),
            1 => targets[0],
            _ => throw new ConfigurationException("TARGET_REQUIRED", "the config declares several targets (" + names + "); " + howToName),
        };
    }
}
