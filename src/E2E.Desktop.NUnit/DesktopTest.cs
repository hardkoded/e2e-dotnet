// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.NUnit;

namespace E2E.Desktop.NUnit;

/// <summary>
/// Base class for a test that drives a native macOS app through the desktop engine. The app comes from <c>targets[0]</c> of
/// <c>e2e.config.json</c>: <c>{ "platform": "macos", "app": { "bundleId": "..." } }</c> or <c>{ "platform": "macos", "app": { "appPath": "build/Example.app" } }</c>.
/// A test launches it with <c>App.OpenAsync()</c>, and every test ends with the app quit. The run needs a Mac with Accessibility permission granted to the
/// process that starts it (and Screen Recording, for screenshots). Upstream's <c>test</c> of <c>@e2e-dev/desktop</c> is <c>e2e</c>'s <c>test</c> extended
/// with the <c>desktop</c> fixture; the port's form is this base class.
/// </summary>
public abstract class DesktopTest : E2ETest
{
    private DesktopEngine? _engine;

    /// <summary>The <c>desktop</c> fixture of the test's session: the app's windows, a key into the app, quitting it.</summary>
    protected Desktop Desktop =>
        (_engine ?? throw new InvalidOperationException("The engine starts with the test session."))
            .ActiveSessions.Single()
            .Desktop;

    /// <summary>The options of the engine: what an observation captures, whether physical input is allowed, how long to settle, how deep to read.</summary>
    protected virtual DesktopOptions CreateDesktopOptions() => new();

    /// <summary>A <see cref="DesktopEngine"/> over the app the target names.</summary>
    protected override IEngine CreateEngine() => _engine = new DesktopEngine(CreateDesktopOptions());

    /// <summary>A desktop app is slower than a browser: each action waits for the window to settle, and the model sees a new tree after it.</summary>
    protected override TimeSpan StepTimeout => TimeSpan.FromSeconds(90);
}
