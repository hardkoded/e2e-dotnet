// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.XUnit.V3;

namespace E2E.WinApp.XUnit.V3;

/// <summary>
/// Base class for an xUnit v3 test that drives a Windows desktop app. It launches the app fresh before each test and ends it after.
/// The app comes from <c>targets[0]</c> of <c>e2e.config.json</c>: <c>{ "platform": "windows", "app": { "appPath": "...", "bundleId": "..." } }</c>,
/// where <c>appPath</c> is the executable to launch and <c>bundleId</c> its process name. Override <see cref="AppId"/> or <see cref="AppPath"/>
/// to set them in code. The tests need an unlocked interactive desktop: keys and clicks go to the window in the foreground.
/// <para><c>E2E_WINAPP_APP_PATH</c> names the executable over the config, so one config serves a build in another place.</para>
/// </summary>
public abstract class WinAppTest : E2ETest
{
    private WinAppEngine? _engine;

    /// <summary>The app's process name: <c>targets[0].app.bundleId</c>. Null when the target names the executable alone.</summary>
    protected virtual string? AppId => WinAppHost.AppId(Config);

    /// <summary>The executable the run launches, relative to the folder of <c>e2e.config.json</c>: <c>E2E_WINAPP_APP_PATH</c>, else <c>targets[0].app.appPath</c>. Null attaches to the running app <see cref="AppId"/> names.</summary>
    protected virtual string? AppPath => WinAppHost.AppPath(Config);

    /// <summary>The window fixture of the test's session: open and close the app, resize and maximize its window, answer a dialog.</summary>
    protected Window Window => (_engine ?? throw new InvalidOperationException("The engine starts with the test session.")).ActiveSessions.Single().Window;

    /// <summary>The options of the engine.</summary>
    protected virtual WinAppOptions CreateWinAppOptions() => WinAppHost.Options(Config, AppId, AppPath);

    /// <summary>A <see cref="WinAppEngine"/> over the app the target names.</summary>
    protected override IEngine CreateEngine() => _engine = new WinAppEngine(CreateWinAppOptions());

    /// <summary>The agent can open another app and answer a dialog.</summary>
    protected override IReadOnlyDictionary<string, AgentTool>? AgentTools => WinAppTools.Create(_engine ?? throw new InvalidOperationException("The engine starts with the test session."));

    /// <summary>A desktop app is slower than a browser: each action waits for the window to settle, and the model sees a new tree after it.</summary>
    protected override TimeSpan StepTimeout => TimeSpan.FromSeconds(90);

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await WinAppHost.OpenAsync(Window, Xunit.TestContext.Current.CancellationToken);
    }
}
