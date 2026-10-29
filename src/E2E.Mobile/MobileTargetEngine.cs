// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Mobile;

/// <summary>
/// Builds the <see cref="MobileEngine"/> of an <c>ios</c> or <c>android</c> config target, for <c>e2e explore</c> and <c>e2e mcp</c>.
/// There is no upstream counterpart: upstream's config holds a live engine handle, provider included. The target's platform and its
/// <c>app.bundleId</c> or <c>app.appPath</c> decide the engine, as for the NUnit and xUnit adapters; <c>E2E_MOBILE_DEVICE</c> names the
/// simulator or emulator, and <c>E2E_MOBILE_PROVIDER</c> names a hosted provider, which the host supplies. The engine leases the hosted
/// device when its session opens and releases it when the session closes.
/// </summary>
public static class MobileTargetEngine
{
    /// <summary>Builds the engine of <paramref name="target"/>, which drives a local device.</summary>
    /// <exception cref="ConfigurationException"><c>INVALID_CONFIG</c>: the platform is not a device, the app is missing, or <c>E2E_MOBILE_PROVIDER</c> asks for a hosted device this host has none of.</exception>
    public static MobileEngine Create(TargetConfig target) => Create(target, Environment.GetEnvironmentVariable, null);

    /// <summary>
    /// Builds the engine of <paramref name="target"/>. With <c>E2E_MOBILE_PROVIDER</c> set to a name other than <c>local</c>,
    /// <paramref name="hostedProvider"/> makes that provider, or throws <c>INVALID_CONFIG</c> for a name it does not know.
    /// </summary>
    public static MobileEngine Create(TargetConfig target, Func<string, IDeviceProvider> hostedProvider)
    {
        ArgumentNullException.ThrowIfNull(hostedProvider);
        return Create(target, Environment.GetEnvironmentVariable, hostedProvider);
    }

    internal static MobileEngine Create(TargetConfig target, Func<string, string?> environment, Func<string, IDeviceProvider>? hostedProvider)
    {
        ArgumentNullException.ThrowIfNull(target);
        var platform = target.Platform switch
        {
            "ios" => MobilePlatform.Ios,
            "android" => MobilePlatform.Android,
            _ => throw new ConfigurationException("INVALID_CONFIG", $"target \"{target.Name}\" has platform {target.Platform}, not a device platform (ios or android)"),
        };
        var app = new MobileApp
        {
            BundleId = target.App.BundleId,
            AppPath = target.App.AppPath,
            LaunchArguments = target.App.LaunchArguments,
            Permissions = DevicePermissionsOf(target.Name, target.App.Permissions),
        };
        MobileEngine.ValidateApp(app, target.App.Url, target.Name, platform);
        IDeviceProvider? provider = null;
        if (environment("E2E_MOBILE_PROVIDER") is { Length: > 0 } name && !string.Equals(name, "local", StringComparison.OrdinalIgnoreCase))
        {
            provider = hostedProvider is null
                ? throw new ConfigurationException("INVALID_CONFIG", $"E2E_MOBILE_PROVIDER={name} asks for a hosted device, and this host has no hosted device provider; unset it to drive a local simulator or emulator")
                : hostedProvider(name.ToLowerInvariant());
        }

        // The provider leases the device, so a named local one would be a second choice.
        return new MobileEngine(new MobileOptions
        {
            Platform = platform,
            Device = provider is null && environment("E2E_MOBILE_DEVICE") is { Length: > 0 } device ? device : null,
            Provider = provider,
            TargetName = target.Name,
            App = app,
        });
    }

    /// <summary>
    /// The target's <c>app.permissions</c> as the device names them, for <see cref="MobileApp.Permissions"/>. A name agent-device does not know
    /// is <c>INVALID_CONFIG</c>, with the nearest one (upstream's <c>assertPermissions</c>, which the mobile engine's <c>validateApp</c> runs).
    /// </summary>
    /// <exception cref="ConfigurationException"><c>INVALID_CONFIG</c>: a permission name the device does not know.</exception>
    public static Dictionary<DevicePermission, PermissionState>? DevicePermissionsOf(string targetName, IReadOnlyDictionary<string, AppPermissionState>? permissions)
    {
        if (permissions is null)
        {
            return null;
        }

        var known = Enum.GetValues<DevicePermission>().ToDictionary(permission => permission.WireName(), StringComparer.Ordinal);
        var names = known.Keys.ToList();
        var resolved = new Dictionary<DevicePermission, PermissionState>();
        foreach (var (name, state) in permissions)
        {
            if (!known.TryGetValue(name, out var permission))
            {
                var hint = Suggestions.DidYouMean(name, names);
                throw new ConfigurationException(
                    "INVALID_CONFIG",
                    $"target \"{targetName}\" app.permissions has unknown key \"{name}\"" + (hint.Length == 0 ? "; expected one of " + string.Join(", ", names) : hint));
            }

            resolved[permission] = state switch
            {
                AppPermissionState.Grant => PermissionState.Grant,
                AppPermissionState.Deny => PermissionState.Deny,
                _ => PermissionState.Reset,
            };
        }

        return resolved;
    }
}
