// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;

namespace E2E.Desktop.Tests.TargetEngine;

/// <summary>.NET-only: upstream's config holds a live engine handle (<c>desktop()</c>); the port's config names the <c>macos</c> platform and <see cref="DesktopTargetEngine"/> builds the engine of it.</summary>
public sealed class DesktopTargetEngineTests
{
    private static TargetConfig Target(string platform, AppConfig app) =>
        new()
        {
            Name = "mac",
            Platform = platform,
            App = app,
        };

    [Fact]
    public void Builds_the_engine_of_a_macos_target()
    {
        var engine = DesktopTargetEngine.Create(Target("macos", new AppConfig { BundleId = "com.apple.calculator" }));
        Assert.Equal("macos", engine.Platform);
        Assert.Equal("desktop", engine.Name);
    }

    [Fact]
    public void Refuses_a_target_of_another_platform()
    {
        var error = Assert.Throws<ConfigurationException>(() =>
            DesktopTargetEngine.Create(Target("windows", new AppConfig { BundleId = "com.apple.calculator" }))
        );
        Assert.Equal("INVALID_CONFIG", error.Code);
    }

    [Fact]
    public void Refuses_an_app_the_engine_cannot_launch_at_config_load()
    {
        var error = Assert.Throws<ConfigurationException>(() =>
            DesktopTargetEngine.Create(Target("macos", new AppConfig()))
        );
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("target \"mac\" needs app.bundleId or app.appPath", error.Message);
    }
}
