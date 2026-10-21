// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.TargetEngineChoice;

/// <summary>
/// .NET-only: upstream's config holds a live engine handle, so it has no code that picks an engine from a target and no <c>it</c> for it.
/// </summary>
public sealed class TargetEngineChoiceTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-target-engines-root");

    private static TargetConfig Target(string platform) => new() { Name = "t", Platform = platform, App = new AppConfig { BundleId = "com.example.app" } };

    private sealed class DeviceEngine(string platform) : IEngine
    {
        public string Platform => platform;

        public string Version => "1";

        public EngineCapabilities Capabilities => EngineCapabilities.PinnedApp;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    [Fact]
    public void Builds_a_web_engine_for_a_web_target_without_asking_for_a_device_engine()
    {
        var engine = TargetEngines.Create(Target("web"), headless: true, mobile: _ => throw new InvalidOperationException("no device engine for web"));
        Assert.IsType<WebEngine>(engine);
    }

    [Theory]
    [InlineData("ios")]
    [InlineData("android")]
    public void Builds_the_device_engine_of_the_host_for_an_ios_or_android_target(string platform)
    {
        var target = Target(platform);
        TargetConfig? seen = null;
        var engine = TargetEngines.Create(target, headless: null, mobile: given =>
        {
            seen = given;
            return new DeviceEngine(platform);
        });
        Assert.Same(target, seen);
        Assert.Equal(platform, engine.Platform);
    }

    /// <summary>.NET-only: upstream's <c>launchesPinnedApp</c> reads the engine handle of the target; the port reads the engine it built.</summary>
    [Fact]
    public void Launches_a_pinned_app_only_for_a_target_without_a_url_on_an_engine_that_pins_one()
    {
        var device = new DeviceEngine("ios");
        Assert.True(TargetEngines.LaunchesPinnedApp(Target("ios"), device));
        Assert.False(TargetEngines.LaunchesPinnedApp(new TargetConfig { Name = "t", Platform = "ios", App = new AppConfig { Url = "https://app.test" } }, device));
        Assert.False(TargetEngines.LaunchesPinnedApp(Target("web"), new WebEngine(true)));
    }

    [Fact]
    public void Refuses_a_device_target_when_the_host_has_no_device_engine()
    {
        var error = Assert.Throws<ConfigurationException>(() => TargetEngines.Create(Target("ios"), headless: null, mobile: null));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("has no device engine", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_platform_that_is_not_web_ios_or_android()
    {
        var error = Assert.Throws<ConfigurationException>(() => TargetEngines.Create(Target("tvos"), headless: null, mobile: _ => new DeviceEngine("tvos")));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("the platform must be web, ios, or android", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Still_rejects_an_engine_key_on_a_target()
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "targets": [{ "platform": "ios", "engine": "mobile", "app": { "bundleId": "com.example.app" } }] }""", Root, _ => null));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("\"engine\"", error.Message, StringComparison.Ordinal);
    }
}
