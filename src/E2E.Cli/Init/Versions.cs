// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace E2E.Cli;

/// <summary>
/// Upstream's <c>cli/init/versions.ts</c>. Every package of this repo is released at one version (MinVer), so the version
/// of this tool stands for the runner and for every engine; upstream's recorded sibling versions have no counterpart.
/// </summary>
internal static partial class Versions
{
    /// <summary>The version of this tool as upstream's <c>packageVersion</c> reads it: no build metadata, or null when the build carries none.</summary>
    public static string? RunnerVersion { get; } = typeof(Versions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];

    /// <summary>
    /// The NuGet version range init writes for an E2E package: the minor of the version released alongside this tool (upstream's
    /// caret, <c>[0.7.0,0.8.0)</c>), since a bare version would let NuGet pick any newer release. A prerelease pins itself
    /// exactly (<c>[0.8.0-preview.1]</c>), because a range on a prerelease resolves to a different build. From a build with
    /// no version, any <c>0.*</c> (upstream's <c>0.x</c>).
    /// </summary>
    public static string DependencyRange(string? version)
    {
        if (version is null)
        {
            return "0.*";
        }

        var match = Stable().Match(version);
        if (!match.Success)
        {
            return "[" + version + "]";
        }

        var major = int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
        var minor = int.Parse(match.Groups[2].ValueSpan, CultureInfo.InvariantCulture);
        var patch = int.Parse(match.Groups[3].ValueSpan, CultureInfo.InvariantCulture);

        // A caret keeps the leftmost non-zero part: ^1.2.3 stops below 2.0.0, ^0.7.0 below 0.8.0, ^0.0.3 below 0.0.4.
        var upper = major > 0 ? $"{major + 1}.0.0" : minor > 0 ? $"0.{minor + 1}.0" : $"0.0.{patch + 1}";
        return $"[{version},{upper})";
    }

    [GeneratedRegex(@"\A(\d+)\.(\d+)\.(\d+)\z")]
    private static partial Regex Stable();
}
