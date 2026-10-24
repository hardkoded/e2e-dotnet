// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using E2E.WinApp;

namespace E2E.Cli;

/// <summary>
/// The Windows app engine of a config target in <c>e2e explore</c> and <c>e2e mcp</c>. There is no upstream counterpart: upstream has no
/// desktop engine. The engine drives the app through Windows UI Automation, so a <c>windows</c> target is refused on any other host
/// before a session opens.
/// </summary>
internal static class WindowsTargets
{
    /// <summary>Builds the engine of <paramref name="target"/>. The app is launched when the session opens, not here.</summary>
    public static WinAppEngine Create(TargetConfig target) => Create(target, Environment.GetEnvironmentVariable, OperatingSystem.IsWindows());

    internal static WinAppEngine Create(TargetConfig target, Func<string, string?> environment, bool isWindows)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!isWindows)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"target \"{target.Name}\" has platform windows, which drives a desktop app through Windows UI Automation and runs on Windows only; this host is {RuntimeInformation.OSDescription}");
        }

        return WinAppTargetEngine.Create(target, environment);
    }
}
