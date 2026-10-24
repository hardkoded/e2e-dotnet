// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.WinApp;

/// <summary>
/// What <c>WinAppTest</c> of each test framework shares: the options of the engine and the launch before each test. Compiled into
/// <c>E2E.WinApp.NUnit</c> and <c>E2E.WinApp.XUnit.V3</c>. There is no upstream counterpart: the framework adapters are .NET-only.
/// </summary>
internal static class WinAppHost
{
    /// <summary><c>E2E_WINAPP_APP_PATH</c>, else the target's <c>app.appPath</c>.</summary>
    internal static string? AppPath(E2EConfig config) =>
        Environment.GetEnvironmentVariable(WinAppTargetEngine.AppPathVariable) is { Length: > 0 } path ? path : config.Target.App.AppPath;

    /// <summary>The target's <c>app.bundleId</c>: the process name the app is known by.</summary>
    internal static string? AppId(E2EConfig config) => config.Target.App.BundleId;

    /// <summary>The options of the engine.</summary>
    internal static WinAppOptions Options(E2EConfig config, string? appId, string? appPath)
    {
        if (appId is null && appPath is null)
        {
            throw new InvalidOperationException($"Set targets[0].app.appPath or targets[0].app.bundleId in e2e.config.json, {WinAppTargetEngine.AppPathVariable}, or override WinAppTest.AppPath.");
        }

        return new WinAppOptions
        {
            TargetName = config.Target.Name,
            App = new WinAppApp { BundleId = appId, AppPath = appPath },
        };
    }

    /// <summary>Launches the app fresh for one test: the core <c>App.OpenAsync</c> takes a URL, so a Windows target launches through the window fixture.</summary>
    internal static Task OpenAsync(Window window, CancellationToken cancellationToken) =>
        window.OpenAppAsync(null, new OpenAppOptions { Relaunch = true }, cancellationToken);
}
