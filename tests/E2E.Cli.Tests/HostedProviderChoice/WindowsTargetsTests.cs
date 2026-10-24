// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Targets = E2E.Cli.WindowsTargets;

namespace E2E.Cli.Tests.HostedProviderChoice;

/// <summary>.NET-only: upstream has no desktop engine, so it has no code that makes one from a config target.</summary>
public sealed class WindowsTargetsTests
{
    private static readonly TargetConfig Target = new() { Name = "desk", Platform = "windows", App = new AppConfig { AppPath = "bin/Desk.exe", BundleId = "Desk" } };

    private static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
        name => variables.Where(variable => variable.Name == name).Select(variable => variable.Value).FirstOrDefault();

    [Fact]
    public void Builds_the_windows_engine_of_the_target_on_windows()
    {
        var engine = Targets.Create(Target, Environment(), isWindows: true);
        Assert.Equal("windows", engine.Platform);
        Assert.Equal("desk", engine.Options.TargetName);
        Assert.Equal("bin/Desk.exe", engine.Options.App.AppPath);
        Assert.Equal("Desk", engine.Options.App.BundleId);
    }

    [Fact]
    public void Lets_E2E_WINAPP_APP_PATH_name_the_build_over_the_target()
    {
        var engine = Targets.Create(Target, Environment(("E2E_WINAPP_APP_PATH", "out/Desk.exe")), isWindows: true);
        Assert.Equal("out/Desk.exe", engine.Options.App.AppPath);
    }

    [Fact]
    public void Refuses_a_windows_target_on_another_host_as_a_config_error()
    {
        var error = Assert.Throws<ConfigurationException>(() => Targets.Create(Target, Environment(), isWindows: false));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("runs on Windows only", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_windows_target_without_an_app()
    {
        var target = new TargetConfig { Name = "desk", Platform = "windows", App = new AppConfig() };
        var error = Assert.Throws<ConfigurationException>(() => Targets.Create(target, Environment(), isWindows: true));
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains("needs app.appPath or app.bundleId", error.Message, StringComparison.Ordinal);
    }
}
