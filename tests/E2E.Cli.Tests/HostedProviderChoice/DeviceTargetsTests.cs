// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Mobile;
using E2E.Mobile.DevFlow;
using Targets = E2E.Cli.DeviceTargets;

namespace E2E.Cli.Tests.HostedProviderChoice;

/// <summary>.NET-only: upstream's config holds the provider itself, so it has no code that makes one from an environment variable.</summary>
public sealed class DeviceTargetsTests
{
    private static readonly TargetConfig Target = new() { Name = "shop", Platform = "ios", App = new AppConfig { BundleId = "com.example.shop" } };

    private static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
        name => variables.Where(variable => variable.Name == name).Select(variable => variable.Value).FirstOrDefault();

    [Fact]
    public void Drives_a_local_device_without_E2E_MOBILE_PROVIDER()
    {
        var engine = Targets.Create(Target, Environment());
        Assert.Null(engine.Options.Provider);
    }

    [Theory]
    [InlineData("limrun", "limrun")]
    [InlineData("LimRun", "limrun")]
    [InlineData("devflow", "devflow")]
    public void Makes_the_hosted_provider_the_variable_names(string value, string expected)
    {
        var engine = Targets.Create(Target, Environment(("E2E_MOBILE_PROVIDER", value)));
        Assert.Equal(expected, engine.Options.Provider?.Name);
        Assert.Null(engine.Options.Device);
    }

    [Fact]
    public void Tells_a_devflow_provider_to_attach_and_a_limrun_one_to_relaunch()
    {
        Assert.True(Targets.Create(Target, Environment(("E2E_MOBILE_PROVIDER", "devflow"))).Options.Provider!.AttachesToRunningApp);
        Assert.False(Targets.Create(Target, Environment(("E2E_MOBILE_PROVIDER", "limrun"))).Options.Provider!.AttachesToRunningApp);
        Assert.IsType<DevFlowAgents>(Targets.Create(Target, Environment(("E2E_MOBILE_PROVIDER", "devflow"))).Options.Provider);
    }

    [Fact]
    public void Refuses_a_provider_it_does_not_ship_and_names_the_ones_it_does()
    {
        var error = Assert.Throws<ConfigurationException>(() => Targets.Create(Target, Environment(("E2E_MOBILE_PROVIDER", "eas"))));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal("E2E_MOBILE_PROVIDER must be local, limrun or devflow, not eas", error.Message);
    }

    [Fact]
    public void Refuses_a_devflow_port_that_is_not_a_port_as_a_config_error()
    {
        var error = Assert.Throws<ConfigurationException>(() => Targets.Create(Target, Environment(("E2E_MOBILE_PROVIDER", "devflow"), ("E2E_DEVFLOW_PORT", "99999"))));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("E2E_DEVFLOW_PORT must be a port number", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Treats_the_limrun_key_as_a_secret_and_skips_a_value_too_short_to_redact()
    {
        var secrets = Targets.Credentials(Environment(("LIMRUN_API_KEY", "limrun-key-12345")));
        var secret = Assert.Single(secrets);
        Assert.Equal("LIMRUN_API_KEY", secret.Name);
        Assert.Empty(Targets.Credentials(Environment(("LIMRUN_API_KEY", "abc"))));
        Assert.Empty(Targets.Credentials(Environment()));
    }
}
