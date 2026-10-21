// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Mobile.Tests.TargetEngine;

/// <summary>
/// .NET-only: upstream's config holds a live engine handle, so it has no code that builds a device engine from a target and no <c>it</c> for it.
/// </summary>
public sealed class MobileTargetEngineTests
{
    private static TargetConfig Target(string platform, AppConfig app) => new() { Name = "shop", Platform = platform, App = app };

    private static string? NoEnvironment(string name) => null;

    [Theory]
    [InlineData("ios", MobilePlatform.Ios)]
    [InlineData("android", MobilePlatform.Android)]
    public void Builds_the_engine_of_the_targets_platform_and_app(string platform, MobilePlatform expected)
    {
        var engine = MobileTargetEngine.Create(Target(platform, new AppConfig { BundleId = "com.example.shop", AppPath = "build/shop.app" }), NoEnvironment);
        Assert.Equal(expected, engine.Options.Platform);
        Assert.Equal("shop", engine.Options.TargetName);
        Assert.Equal("com.example.shop", engine.Options.App.BundleId);
        Assert.Equal("build/shop.app", engine.Options.App.AppPath);
        Assert.Null(engine.Options.Device);
        Assert.Null(engine.Options.Provider);
        Assert.True(engine.Capabilities.HasFlag(EngineCapabilities.PinnedApp));
    }

    [Fact]
    public void Takes_a_build_path_alone_as_the_app()
    {
        var engine = MobileTargetEngine.Create(Target("ios", new AppConfig { AppPath = "build/shop.app" }), NoEnvironment);
        Assert.Null(engine.Options.App.BundleId);
        Assert.Equal("build/shop.app", engine.Options.App.AppPath);
    }

    [Fact]
    public void Names_the_device_from_E2E_MOBILE_DEVICE()
    {
        var engine = MobileTargetEngine.Create(Target("ios", new AppConfig { BundleId = "com.example.shop" }), name => name == "E2E_MOBILE_DEVICE" ? "iPhone 16" : null);
        Assert.Equal("iPhone 16", engine.Options.Device);
    }

    [Fact]
    public void Refuses_a_target_with_neither_bundleId_nor_appPath()
    {
        var error = Assert.Throws<ConfigurationException>(() => MobileTargetEngine.Create(Target("android", new AppConfig()), NoEnvironment));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("needs app.bundleId or app.appPath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_platform_that_is_not_a_device()
    {
        var error = Assert.Throws<ConfigurationException>(() => MobileTargetEngine.Create(Target("web", new AppConfig { BundleId = "com.example.shop" }), NoEnvironment));
        Assert.Equal("INVALID_CONFIG", error.Code);
    }

    [Theory]
    [InlineData("limrun")]
    [InlineData("devflow")]
    public void Refuses_a_hosted_provider_that_explore_and_mcp_do_not_support_yet(string provider)
    {
        var error = Assert.Throws<ConfigurationException>(() => MobileTargetEngine.Create(
            Target("ios", new AppConfig { BundleId = "com.example.shop" }),
            name => name == "E2E_MOBILE_PROVIDER" ? provider : null));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("hosted device", error.Message, StringComparison.Ordinal);
        Assert.Contains("do not support yet", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Drives_a_local_device_when_the_provider_is_local()
    {
        var engine = MobileTargetEngine.Create(Target("ios", new AppConfig { BundleId = "com.example.shop" }), name => name == "E2E_MOBILE_PROVIDER" ? "local" : null);
        Assert.Null(engine.Options.Provider);
    }
}
