// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile;

/// <summary>
/// Builds the <see cref="MobileEngine"/> of an <c>ios</c> or <c>android</c> config target, for <c>e2e explore</c> and <c>e2e mcp</c>.
/// There is no upstream counterpart: upstream's config holds a live engine handle. The target's platform and its
/// <c>app.bundleId</c> or <c>app.appPath</c> decide the engine, as for the NUnit and xUnit adapters; <c>E2E_MOBILE_DEVICE</c> names the
/// simulator or emulator. Hosted providers are not supported yet.
/// </summary>
public static class MobileTargetEngine
{
    /// <summary>Builds the engine of <paramref name="target"/>, which drives a local device.</summary>
    /// <exception cref="ConfigurationException"><c>INVALID_CONFIG</c>: the platform is not a device, the app is missing, or <c>E2E_MOBILE_PROVIDER</c> asks for a hosted device.</exception>
    public static MobileEngine Create(TargetConfig target) => Create(target, Environment.GetEnvironmentVariable);

    internal static MobileEngine Create(TargetConfig target, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (environment("E2E_MOBILE_PROVIDER") is { Length: > 0 } provider && !string.Equals(provider, "local", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"E2E_MOBILE_PROVIDER={provider} asks for a hosted device, which e2e explore and e2e mcp do not support yet; unset it to drive a local simulator or emulator");
        }

        var platform = target.Platform switch
        {
            "ios" => MobilePlatform.Ios,
            "android" => MobilePlatform.Android,
            _ => throw new ConfigurationException("INVALID_CONFIG", $"target \"{target.Name}\" has platform {target.Platform}, not a device platform (ios or android)"),
        };
        var app = new MobileApp { BundleId = target.App.BundleId, AppPath = target.App.AppPath };
        MobileEngine.ValidateApp(app, target.App.Url, target.Name, platform);
        return new MobileEngine(new MobileOptions
        {
            Platform = platform,
            Device = environment("E2E_MOBILE_DEVICE") is { Length: > 0 } device ? device : null,
            TargetName = target.Name,
            App = app,
        });
    }
}
