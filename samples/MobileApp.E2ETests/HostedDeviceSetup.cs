// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using E2E;
using E2E.Limrun;
using E2E.Mobile;
using E2E.Mobile.DevFlow;

namespace MobileApp.E2ETests;

/// <summary>
/// With <c>E2E_MOBILE_PROVIDER=limrun</c>, leases one hosted device for the whole run and releases it at the end. With
/// <c>E2E_MOBILE_PROVIDER=devflow</c>, installs and launches the app on the booted simulator or emulator, holds its MAUI DevFlow
/// agent's mutation lease for the whole run, and releases it at the end. The NUnit adapter starts one engine session per test and does not run the engine's prepare and
/// finish hooks, so this does: every test shares the engine, and so the device. Without the variable (or with <c>local</c>) it
/// does nothing.
/// </summary>
[SetUpFixture]
public sealed class HostedDeviceSetup
{
    private static MobileEngine? s_engine;
    private static Dictionary<string, string?> s_env = [];
    private static string s_runId = "";
    private static string s_target = "";

    /// <summary>The engine that owns the leased device, or null when the tests drive a local one.</summary>
    internal static MobileEngine? Engine => s_engine;

    /// <summary>True when an iOS app is driven through the DevFlow agent: the simulator has no home button for it, so the one test that needs it is inconclusive.</summary>
    internal static bool DrivesIosThroughDevFlow { get; private set; }

    [OneTimeSetUp]
    public async Task LeaseDeviceAsync()
    {
        var name = Environment.GetEnvironmentVariable("E2E_MOBILE_PROVIDER")?.ToLowerInvariant();
        if (name is null or "" or "local")
        {
            return;
        }

        IDeviceProvider provider = name switch
        {
            // A hosted device that outlives an interrupted run is billed until its hard timeout.
            "limrun" => new LimrunSimulators(new LimrunSimulatorsOptions { HardTimeout = "15m", Ios = new LimrunIosOptions { Model = LimrunIosModel.Iphone } }),
            // The agent inside the Debug build of samples/MobileApp, launched on E2E_MOBILE_DEVICE or the booted device. E2E_DEVFLOW_PORT names its port; without it the broker is asked, then 9223.
            "devflow" => new DevFlowAgents(new DevFlowAgentsOptions
            {
                Port = int.TryParse(Environment.GetEnvironmentVariable("E2E_DEVFLOW_PORT"), out var port) ? port : null,
                AppName = "MobileApp",
                Device = Environment.GetEnvironmentVariable("E2E_MOBILE_DEVICE") is { Length: > 0 } device ? device : null,
            }),
            _ => throw new InvalidOperationException($"E2E_MOBILE_PROVIDER must be local, limrun, or devflow, not {name}."),
        };
        DrivesIosThroughDevFlow = name == "devflow" && !string.Equals(Environment.GetEnvironmentVariable("E2E_MOBILE_PLATFORM"), "android", StringComparison.OrdinalIgnoreCase);
        var options = MobileAppTest.Options(provider);
        s_env = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value, StringComparer.Ordinal);
        s_runId = Guid.NewGuid().ToString("N")[..12];
        s_target = options.TargetName;
        // The root the local path uses: the folder of e2e.config.json. The test run's current directory is the build output.
        var projectRoot = E2EConfig.Discover().ProjectRoot;
        var engine = new MobileEngine(options);
        try
        {
            await engine.PrepareAsync(
                new MobilePrepareInfo { RunId = s_runId, TargetName = options.TargetName, ProjectRoot = projectRoot, Slots = 1, Env = s_env, App = options.App, Log = NUnit.Framework.TestContext.Progress.WriteLine },
                NUnit.Framework.TestContext.CurrentContext.CancellationToken);
        }
        catch
        {
            // Release what prepare got to, and let the failure of prepare reach the user, not a failure of the release.
            try
            {
                await FinishAsync(engine);
            }
            catch (Exception cleanup)
            {
                NUnit.Framework.TestContext.Progress.WriteLine($"Releasing the hosted device failed: {cleanup.Message}");
            }

            throw;
        }

        s_engine = engine;
    }

    [OneTimeTearDown]
    public async Task ReleaseDeviceAsync()
    {
        if (s_engine is { } engine)
        {
            s_engine = null;
            await FinishAsync(engine);
        }
    }

    private static Task FinishAsync(MobileEngine engine) =>
        engine.FinishAsync(new MobileFinishInfo { RunId = s_runId, TargetName = s_target, Env = s_env, Log = NUnit.Framework.TestContext.Progress.WriteLine }, CancellationToken.None);
}
