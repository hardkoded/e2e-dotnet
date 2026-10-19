// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using E2E.Limrun;
using E2E.Mobile.DevFlow;

namespace E2E.Mobile;

/// <summary>
/// What <c>MobileAppTest</c> of each test framework shares: the options of the engine, the one hosted device a run leases,
/// and the install of the build. Compiled into <c>E2E.Mobile.NUnit</c> and <c>E2E.Mobile.XUnit.V3</c>. There is no upstream
/// counterpart: the framework adapters are .NET-only.
/// </summary>
internal static class MobileAppHost
{
    // The device keeps the build between tests, so only the first test of the run installs it.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool s_installed;
    private static bool s_leased;
    private static MobileEngine? s_engine;
    private static Dictionary<string, string?> s_env = [];
    private static string s_runId = "";
    private static string s_target = "";
    private static Action<string> s_log = _ => { };

    /// <summary>The engine that owns the leased device, or null when the tests drive a local one.</summary>
    internal static MobileEngine? Engine => s_engine;

    /// <summary>True when the tests attach to an app that is already running (the DevFlow agent): there is no build to install and no relaunch.</summary>
    internal static bool AttachesToRunningApp { get; private set; }

    /// <summary><c>E2E_MOBILE_PLATFORM</c>, else the target's platform. A value other than <c>ios</c> or <c>android</c> fails the run.</summary>
    internal static MobilePlatform Platform(E2EConfig config)
    {
        var name = Environment.GetEnvironmentVariable("E2E_MOBILE_PLATFORM") is { Length: > 0 } variable ? variable : config.Target.Platform;
        return name.ToLowerInvariant() switch
        {
            "ios" => MobilePlatform.Ios,
            "android" => MobilePlatform.Android,
            _ => throw new InvalidOperationException($"The platform must be ios or android, not {name}. Set targets[0].platform in e2e.config.json, or E2E_MOBILE_PLATFORM."),
        };
    }

    /// <summary><c>E2E_MOBILE_APP_PATH</c>, else the target's <c>app.appPath</c>.</summary>
    internal static string? AppPath(E2EConfig config) =>
        Environment.GetEnvironmentVariable("E2E_MOBILE_APP_PATH") is { Length: > 0 } path ? path : config.Target.App.AppPath;

    /// <summary>The target's <c>app.bundleId</c>.</summary>
    internal static string AppId(E2EConfig config) =>
        config.Target.App.BundleId ?? throw new InvalidOperationException("Set targets[0].app.bundleId in e2e.config.json, or override MobileAppTest.AppId.");

    /// <summary><c>E2E_MOBILE_DEVICE</c> names a simulator or emulator. Unset: the engine drives the booted device of the platform.</summary>
    internal static string? DeviceName() => Environment.GetEnvironmentVariable("E2E_MOBILE_DEVICE") is { Length: > 0 } device ? device : null;

    /// <summary>The options of the engine: the local device, or the provider's hosted device.</summary>
    internal static MobileOptions Options(E2EConfig config, MobilePlatform platform, string appId, string? appPath, string? device, IDeviceProvider? provider) => new()
    {
        Platform = platform,
        Device = provider is null ? device : null,
        Provider = provider,
        TargetName = config.Target.Name,
        App = new MobileApp { BundleId = appId, AppPath = appPath },
    };

    /// <summary>
    /// With <c>E2E_MOBILE_PROVIDER=limrun</c> or <c>devflow</c>, leases one hosted device for the whole run, once, and releases it
    /// when the process ends. Every test shares the engine, and so the device. Without the variable (or with <c>local</c>) it does
    /// nothing, and each test makes its own engine.
    /// </summary>
    internal static async Task LeaseAsync(Func<IDeviceProvider?, MobileOptions> options, string projectRoot, Action<string> log, CancellationToken cancellationToken)
    {
        var name = Environment.GetEnvironmentVariable("E2E_MOBILE_PROVIDER")?.ToLowerInvariant();
        if (name is null or "" or "local")
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (s_leased)
            {
                return;
            }

            IDeviceProvider provider = name switch
            {
                // A hosted device that outlives an interrupted run is billed until its hard timeout.
                "limrun" => new LimrunSimulators(new LimrunSimulatorsOptions { HardTimeout = "15m", Ios = new LimrunIosOptions { Model = LimrunIosModel.Iphone } }),
                // The agent inside the running Debug build. E2E_DEVFLOW_PORT names its port; without it the broker is asked, then 9223.
                "devflow" => new DevFlowAgents(new DevFlowAgentsOptions { Port = DevFlowPort(), AppName = Environment.GetEnvironmentVariable("E2E_DEVFLOW_APP") is { Length: > 0 } app ? app : null }),
                _ => throw new InvalidOperationException($"E2E_MOBILE_PROVIDER must be local, limrun, or devflow, not {name}."),
            };
            var mobile = options(provider);
            var engine = new MobileEngine(mobile);
            var env = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value, StringComparer.Ordinal);
            var runId = Guid.NewGuid().ToString("N")[..12];
            try
            {
                await engine.PrepareAsync(
                    new MobilePrepareInfo { RunId = runId, TargetName = mobile.TargetName, ProjectRoot = projectRoot, Slots = 1, Env = env, App = mobile.App, Log = log },
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Release what prepare got to, and let the failure of prepare reach the user, not a failure of the release.
                try
                {
                    await engine.FinishAsync(new MobileFinishInfo { RunId = runId, TargetName = mobile.TargetName, Env = env, Log = log }, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception cleanup)
                {
                    log($"Releasing the hosted device failed: {cleanup.Message}");
                }

                throw;
            }

            AttachesToRunningApp = name == "devflow";
            (s_engine, s_env, s_runId, s_target, s_log, s_leased) = (engine, env, runId, mobile.TargetName, log, true);
            // The adapters have no hook that runs after the last test, so the process end releases the device.
            AppDomain.CurrentDomain.ProcessExit += (_, _) => ReleaseAsync().Wait(TimeSpan.FromMinutes(2));
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Installs the build once per run, then launches the app fresh. A device keeps the build between tests.</summary>
    internal static async Task InstallAndOpenAsync(Device device, string appId, string? appPath, CancellationToken cancellationToken)
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
        await device.OpenAppAsync(appId, new OpenAppOptions { Relaunch = true }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReleaseAsync()
    {
        if (s_engine is { } engine)
        {
            s_engine = null;
            try
            {
                await engine.FinishAsync(new MobileFinishInfo { RunId = s_runId, TargetName = s_target, Env = s_env, Log = s_log }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                // The test host may be gone at process exit, and its log with it.
                try
                {
                    s_log($"Releasing the hosted device failed: {failure.Message}");
                }
                catch (Exception)
                {
                }
            }
        }
    }

    /// <summary><c>E2E_DEVFLOW_PORT</c>, or null when it is unset. A value that is not a port fails the run, not falls back to discovery.</summary>
    private static int? DevFlowPort() => Environment.GetEnvironmentVariable("E2E_DEVFLOW_PORT") switch
    {
        null or "" => null,
        var text when int.TryParse(text, out var port) && port is > 0 and <= 65535 => port,
        var text => throw new InvalidOperationException($"E2E_DEVFLOW_PORT must be a port number, not {text}."),
    };
}
