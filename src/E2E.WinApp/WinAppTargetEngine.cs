// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.WinApp;

/// <summary>
/// Builds the <see cref="WinAppEngine"/> of a <c>windows</c> config target, for <c>e2e explore</c> and <c>e2e mcp</c>. There is no
/// upstream counterpart: upstream's config holds a live engine handle. The target's <c>app.appPath</c> names the executable and
/// <c>app.bundleId</c> a running app's process name, as for the NUnit and xUnit adapters; <c>E2E_WINAPP_APP_PATH</c> overrides the
/// executable, so one config serves a build in another place.
/// </summary>
public static class WinAppTargetEngine
{
    /// <summary>The variable that names the executable over the target's <c>app.appPath</c>.</summary>
    public const string AppPathVariable = "E2E_WINAPP_APP_PATH";

    /// <summary>Builds the engine of <paramref name="target"/>.</summary>
    /// <exception cref="ConfigurationException"><c>INVALID_CONFIG</c>: the platform is not <c>windows</c>, or the app is missing.</exception>
    public static WinAppEngine Create(TargetConfig target) => Create(target, Environment.GetEnvironmentVariable);

    internal static WinAppEngine Create(TargetConfig target, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(environment);
        if (target.Platform != "windows")
        {
            throw new ConfigurationException("INVALID_CONFIG", $"target \"{target.Name}\" has platform {target.Platform}, not windows");
        }

        var app = new WinAppApp
        {
            BundleId = target.App.BundleId,
            AppPath = environment(AppPathVariable) is { Length: > 0 } path ? path : target.App.AppPath,
        };
        WinAppEngine.ValidateApp(app, target.App.Url, target.Name);
        return new WinAppEngine(new WinAppOptions { TargetName = target.Name, App = app });
    }
}
