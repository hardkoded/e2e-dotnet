// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using E2E.Desktop;

namespace E2E.Cli;

/// <summary>
/// The macOS desktop engine of a config target in <c>e2e explore</c> and <c>e2e mcp</c>. There is no upstream counterpart: upstream's config
/// holds a live engine handle. The engine drives the app through the macOS accessibility API, so a <c>macos</c> target is refused on any other host
/// before a session opens.
/// </summary>
internal static class MacosTargets
{
    /// <summary>Builds the engine of <paramref name="target"/>. The app is launched when the session opens, not here.</summary>
    public static DesktopEngine Create(TargetConfig target) => Create(target, OperatingSystem.IsMacOS());

    internal static DesktopEngine Create(TargetConfig target, bool isMacOS)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!isMacOS)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"target \"{target.Name}\" has platform macos, which drives a desktop app through the macOS accessibility API and runs on macOS only; this host is {RuntimeInformation.OSDescription}"
            );
        }

        return DesktopTargetEngine.Create(target);
    }
}
