// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Desktop;

/// <summary>
/// Builds the <see cref="DesktopEngine"/> of a <c>macos</c> config target, for <c>e2e explore</c> and <c>e2e mcp</c>. There is no upstream
/// counterpart: upstream's config holds a live engine handle (<c>desktop()</c>), and the port's <c>e2e.config.json</c> names a platform. The
/// target's <c>app.bundleId</c> and <c>app.appPath</c> reach the engine when a session starts, as for any pinned-app target.
/// </summary>
public static class DesktopTargetEngine
{
    /// <summary>Builds the engine of <paramref name="target"/>.</summary>
    /// <exception cref="ConfigurationException"><c>INVALID_CONFIG</c>: the platform is not <c>macos</c>, or the app is missing.</exception>
    public static DesktopEngine Create(TargetConfig target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Platform != DesktopOptionsResolver.Platform)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"target \"{target.Name}\" has platform {target.Platform}, not macos"
            );
        }

        var engine = new DesktopEngine();
        ((IEngine)engine).ValidateApp(
            new EngineAppDeclaration
            {
                Url = target.App.Url,
                BundleId = target.App.BundleId,
                AppPath = target.App.AppPath,
                LaunchArguments = target.App.LaunchArguments,
                LaunchArgumentValues = target
                    .App.LaunchArgumentValues?.Where(pair => pair.Value is not null)
                    .ToDictionary<KeyValuePair<string, LaunchArgumentValue?>, string, LaunchArgumentValue>(
                        pair => pair.Key,
                        pair => pair.Value!.Value,
                        StringComparer.Ordinal
                    ),
                Permissions = target.App.Permissions,
                AllPermissions = target.App.AllPermissions,
            },
            new EngineAppCheckInfo { TargetName = target.Name }
        );
        return engine;
    }
}
