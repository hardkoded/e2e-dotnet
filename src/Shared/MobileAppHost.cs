// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile;

/// <summary>
/// What <c>MobileAppTest</c> of each test framework shares: the options of the engine, the one hosted engine a run shares,
/// and the install of the build. Compiled into <c>E2E.Mobile.NUnit</c> and <c>E2E.Mobile.XUnit.V3</c>. There is no upstream
/// counterpart: the framework adapters are .NET-only.
/// </summary>
internal static class MobileAppHost
{
    // The device keeps the build between tests, so only the first test of the run installs it.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    // Guards s_engine and AttachesToRunningApp; Gate above guards s_installed.
    private static readonly Lock EngineLock = new();
    private static bool s_installed;
    private static MobileEngine? s_engine;

    /// <summary>The next <see cref="ShareEngine"/> starts from no shared engine. .NET-only: a test of the host runs in a process that other tests share.</summary>
    internal static void Reset()
    {
        lock (EngineLock)
        {
            s_engine = null;
            AttachesToRunningApp = false;
        }
    }

    /// <summary>The engine whose hosted device the run shares, or null when the tests drive a local one.</summary>
    internal static MobileEngine? Engine => s_engine;

    /// <summary>True when the tests attach to an app that is already running (the DevFlow agent): there is no build to install and no relaunch.</summary>
    internal static bool AttachesToRunningApp { get; private set; }

    /// <summary><c>E2E_MOBILE_PLATFORM</c>, else the target's platform. A value other than <c>ios</c> or <c>android</c> fails the run.</summary>
    internal static MobilePlatform Platform(E2EConfig config)
    {
        var name = Environment.GetEnvironmentVariable("E2E_MOBILE_PLATFORM") is { Length: > 0 } variable
            ? variable
            : config.Target.Platform;
        return name.ToLowerInvariant() switch
        {
            "ios" => MobilePlatform.Ios,
            "android" => MobilePlatform.Android,
            _ => throw new InvalidOperationException(
                $"The platform must be ios or android, not {name}. Set targets[0].platform in e2e.config.json, or E2E_MOBILE_PLATFORM."
            ),
        };
    }

    /// <summary><c>E2E_MOBILE_APP_PATH</c>, else the target's <c>app.appPath</c>.</summary>
    internal static string? AppPath(E2EConfig config) =>
        Environment.GetEnvironmentVariable("E2E_MOBILE_APP_PATH") is { Length: > 0 } path
            ? path
            : config.Target.App.AppPath;

    /// <summary>The target's <c>app.bundleId</c>.</summary>
    internal static string AppId(E2EConfig config) =>
        config.Target.App.BundleId
        ?? throw new InvalidOperationException(
            "Set targets[0].app.bundleId in e2e.config.json, or override MobileAppTest.AppId."
        );

    /// <summary><c>E2E_MOBILE_DEVICE</c> names a simulator or emulator. Unset: the engine drives the booted device of the platform.</summary>
    internal static string? DeviceName() =>
        Environment.GetEnvironmentVariable("E2E_MOBILE_DEVICE") is { Length: > 0 } device ? device : null;

    /// <summary>The options of the engine: the local device, or the provider's hosted device.</summary>
    internal static MobileOptions Options(
        E2EConfig config,
        MobilePlatform platform,
        string appId,
        string? appPath,
        string? device,
        IDeviceProvider? provider
    ) =>
        new()
        {
            Platform = platform,
            Device = provider is null ? device : null,
            Provider = provider,
            TargetName = config.Target.Name,
            App = new MobileApp
            {
                BundleId = appId,
                AppPath = appPath,
                LaunchArguments = config.Target.App.LaunchArguments,
                LaunchArgumentValues = config.Target.App.LaunchArgumentValues,
                Permissions = MobileTargetEngine.DevicePermissionsOf(config.Target.Name, config.Target.App.Permissions),
                AllPermissions = config.Target.App.AllPermissions,
            },
        };

    /// <summary>
    /// With <c>E2E_MOBILE_PROVIDER=limrun</c> or <c>devflow</c>, makes the one engine of the run, whose hosted device every test shares. It
    /// leases nothing itself: the run fixture prepares a shared engine once per process (<c>HostRun.Prepared</c>), which leases the device, and
    /// finishes it when the process ends. A second prepare here would claim the device again under another run id: the DevFlow agent refuses
    /// it as another host's lease, and a Limrun device would be leased twice. Without the variable (or with <c>local</c>) it does nothing, and
    /// each test makes its own engine.
    /// </summary>
    internal static void ShareEngine(Func<IDeviceProvider?, MobileOptions> options)
    {
        var name = Environment.GetEnvironmentVariable("E2E_MOBILE_PROVIDER")?.ToLowerInvariant();
        if (name is null or "" or "local")
        {
            return;
        }

        lock (EngineLock)
        {
            if (s_engine is not null)
            {
                return;
            }

            var provider = HostedDeviceProviders.Create(name, Environment.GetEnvironmentVariable)
                ?? throw new InvalidOperationException(
                    $"E2E_MOBILE_PROVIDER must be local, {HostedDeviceProviders.Names}, not {name}."
                );
            var engine = new MobileEngine(options(provider));
            AttachesToRunningApp = name == "devflow";
            s_engine = engine;
        }
    }

    /// <summary>Installs the build once per run, then launches the app fresh. A device keeps the build between tests.</summary>
    internal static async Task InstallAndOpenAsync(
        Device device,
        string appId,
        string? appPath,
        CancellationToken cancellationToken
    )
    {
        if (AttachesToRunningApp)
        {
            // The DevFlow agent runs inside the app you launched: there is no build to install, and no relaunch yet.
            await device.OpenAppAsync(appId, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!s_installed)
            {
                if (appPath is not null)
                {
                    await device.InstallAppAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                s_installed = true;
            }
        }
        finally
        {
            Gate.Release();
        }

        // The core App.OpenAsync takes a URL. A device target launches the app through the device fixture.
        await device
            .OpenAppAsync(appId, new OpenAppOptions { Relaunch = true }, cancellationToken)
            .ConfigureAwait(false);
    }
}
