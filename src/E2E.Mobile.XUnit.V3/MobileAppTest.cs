// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.XUnit.V3;

namespace E2E.Mobile.XUnit.V3;

/// <summary>
/// Base class for an xUnit v3 test that drives a mobile app. It installs the build on a device once per run and launches the
/// app fresh before each test. The app comes from <c>targets[0]</c> of <c>e2e.config.json</c>:
/// <c>{ "platform": "ios", "app": { "bundleId": "...", "appPath": "..." } }</c>. Override <see cref="AppId"/>,
/// <see cref="AppPath"/>, or <see cref="Platform"/> to set them in code. The tests need a booted simulator or emulator,
/// Node 22.12 or newer for agent-device, and a build of the app.
/// <para>
/// Environment variables pick the device: <c>E2E_MOBILE_PLATFORM</c> (<c>ios</c> or <c>android</c>), <c>E2E_MOBILE_DEVICE</c>,
/// <c>E2E_MOBILE_APP_PATH</c>, and <c>E2E_MOBILE_PROVIDER</c> (<c>local</c>, <c>limrun</c>, or <c>devflow</c>). A hosted run
/// leases one device for all tests and releases it when the process ends.
/// </para>
/// </summary>
public abstract class MobileAppTest : E2ETest
{
    private MobileEngine? _engine;

    /// <summary>The bundle id on iOS and the package on Android: <c>targets[0].app.bundleId</c>.</summary>
    protected virtual string AppId => MobileAppHost.AppId(Config);

    /// <summary>The build the run installs, relative to the folder of <c>e2e.config.json</c>: <c>E2E_MOBILE_APP_PATH</c>, else <c>targets[0].app.appPath</c>. Null launches an installed app.</summary>
    protected virtual string? AppPath => MobileAppHost.AppPath(Config);

    /// <summary><c>E2E_MOBILE_PLATFORM</c>, else <c>targets[0].platform</c>.</summary>
    protected virtual MobilePlatform Platform => MobileAppHost.Platform(Config);

    /// <summary>True when the run attaches to an app that is already running (<c>E2E_MOBILE_PROVIDER=devflow</c>), which cannot be installed or relaunched.</summary>
    protected static bool AttachesToRunningApp => MobileAppHost.AttachesToRunningApp;

    /// <summary>The device fixture of the test's session: appearance, home, foreground app, and the other device controls.</summary>
    protected Device Device => (_engine ?? throw new InvalidOperationException("The engine starts with the test session.")).ActiveSessions.Single().Device;

    /// <summary>The options of the engine: the local device, or the provider's hosted device.</summary>
    protected virtual MobileOptions CreateMobileOptions(IDeviceProvider? provider) =>
        MobileAppHost.Options(Config, Platform, AppId, AppPath, MobileAppHost.DeviceName(), provider);

    /// <summary>The hosted device the run leased, or a <see cref="MobileEngine"/> for the local device.</summary>
    protected override IEngine CreateEngine() => _engine = MobileAppHost.Engine ?? new MobileEngine(CreateMobileOptions(provider: null));

    /// <summary>The agent can open another app, swipe from point to point, and answer a system alert.</summary>
    protected override IReadOnlyDictionary<string, AgentTool>? AgentTools => MobileTools.Create(_engine ?? throw new InvalidOperationException("The engine starts with the test session."));

    /// <summary>A device is slower than a browser: each action waits for the screen to settle, and the model sees a new snapshot after it.</summary>
    protected override TimeSpan StepTimeout => TimeSpan.FromSeconds(90);

    public override async ValueTask InitializeAsync()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        await MobileAppHost.LeaseAsync(CreateMobileOptions, ProjectRoot, Console.WriteLine, cancellationToken);
        await base.InitializeAsync();
        await MobileAppHost.InstallAndOpenAsync(Device, AppId, AppPath, cancellationToken);
    }
}
