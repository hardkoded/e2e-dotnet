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

    private static Func<string, string?> Environment(string provider, string? device = null) =>
        name => name switch
        {
            "E2E_MOBILE_PROVIDER" => provider,
            "E2E_MOBILE_DEVICE" => device,
            _ => null,
        };

    private sealed class NamedProvider(string name) : IDeviceProvider
    {
        public string Name => name;

        public Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("nothing is leased when the engine is built");

        public Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken) => throw new InvalidOperationException("nothing is leased when the engine is built");
    }

    [Theory]
    [InlineData("ios", MobilePlatform.Ios)]
    [InlineData("android", MobilePlatform.Android)]
    public void Builds_the_engine_of_the_targets_platform_and_app(string platform, MobilePlatform expected)
    {
        var engine = MobileTargetEngine.Create(Target(platform, new AppConfig { BundleId = "com.example.shop", AppPath = "build/shop.app" }), NoEnvironment, null);
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
        var engine = MobileTargetEngine.Create(Target("ios", new AppConfig { AppPath = "build/shop.app" }), NoEnvironment, null);
        Assert.Null(engine.Options.App.BundleId);
        Assert.Equal("build/shop.app", engine.Options.App.AppPath);
    }

    [Fact]
    public void Names_the_device_from_E2E_MOBILE_DEVICE()
    {
        var engine = MobileTargetEngine.Create(Target("ios", new AppConfig { BundleId = "com.example.shop" }), name => name == "E2E_MOBILE_DEVICE" ? "iPhone 16" : null, null);
        Assert.Equal("iPhone 16", engine.Options.Device);
    }

    [Fact]
    public void Refuses_a_target_with_neither_bundleId_nor_appPath()
    {
        var error = Assert.Throws<ConfigurationException>(() => MobileTargetEngine.Create(Target("android", new AppConfig()), NoEnvironment, null));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("needs app.bundleId or app.appPath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_platform_that_is_not_a_device()
    {
        var error = Assert.Throws<ConfigurationException>(() => MobileTargetEngine.Create(Target("web", new AppConfig { BundleId = "com.example.shop" }), NoEnvironment, null));
        Assert.Equal("INVALID_CONFIG", error.Code);
    }

    [Theory]
    [InlineData("limrun")]
    [InlineData("devflow")]
    public void Refuses_a_hosted_provider_when_the_host_has_none(string provider)
    {
        var error = Assert.Throws<ConfigurationException>(() => MobileTargetEngine.Create(Target("ios", new AppConfig { BundleId = "com.example.shop" }), Environment(provider), null));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains($"E2E_MOBILE_PROVIDER={provider} asks for a hosted device, and this host has no hosted device provider", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Drives_a_local_device_when_the_provider_is_local()
    {
        var engine = MobileTargetEngine.Create(Target("ios", new AppConfig { BundleId = "com.example.shop" }), Environment("local"), name => throw new InvalidOperationException(name));
        Assert.Null(engine.Options.Provider);
    }

    [Theory]
    [InlineData("limrun")]
    [InlineData("LimRun")]
    public void Leases_from_the_provider_the_host_makes_for_a_hosted_name_and_names_no_local_device(string name)
    {
        var made = new List<string>();
        var engine = MobileTargetEngine.Create(
            Target("android", new AppConfig { BundleId = "com.example.shop" }),
            Environment(name, device: "Pixel 8"),
            provider =>
            {
                made.Add(provider);
                return new NamedProvider(provider);
            });
        Assert.Equal(["limrun"], made);
        Assert.Equal("limrun", engine.Options.Provider?.Name);
        Assert.Null(engine.Options.Device);
        Assert.Equal(MobilePlatform.Android, engine.Options.Platform);
    }

    [Fact]
    public void Lets_the_host_refuse_a_name_it_does_not_know()
    {
        var error = Assert.Throws<ConfigurationException>(() => MobileTargetEngine.Create(
            Target("ios", new AppConfig { BundleId = "com.example.shop" }),
            Environment("nope"),
            name => throw new ConfigurationException("INVALID_CONFIG", $"unknown provider {name}")));
        Assert.Equal("unknown provider nope", error.Message);
    }

    [Fact]
    public void Checks_the_app_before_it_makes_a_provider()
    {
        var error = Assert.Throws<ConfigurationException>(() => MobileTargetEngine.Create(
            Target("ios", new AppConfig()),
            Environment("limrun"),
            name => throw new InvalidOperationException("a provider was made for a target that cannot start")));
        Assert.Contains("needs app.bundleId or app.appPath", error.Message, StringComparison.Ordinal);
    }
}
