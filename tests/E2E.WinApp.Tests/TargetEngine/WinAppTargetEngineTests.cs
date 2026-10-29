// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.WinApp;

namespace E2E.WinApp.Tests.TargetEngine;

/// <summary>.NET-only: upstream's config holds a live engine handle, so it has no code that builds one from a target.</summary>
public sealed class WinAppTargetEngineTests
{
    private static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
        name => variables.Where(variable => variable.Name == name).Select(variable => variable.Value).FirstOrDefault();

    [Fact]
    public void Builds_the_engine_of_a_windows_target_from_its_app()
    {
        var target = new TargetConfig { Name = "desk", Platform = "windows", App = new AppConfig { AppPath = "bin/Desk.exe", BundleId = "Desk" } };
        var engine = WinAppTargetEngine.Create(target, Environment());
        Assert.Equal("desk", engine.Options.TargetName);
        Assert.Equal("bin/Desk.exe", engine.Options.App.AppPath);
        Assert.Equal("Desk", engine.Options.App.BundleId);
    }

    [Fact]
    public void Hands_the_targets_launch_arguments_to_the_app()
    {
        var target = new TargetConfig { Name = "desk", Platform = "windows", App = new AppConfig { AppPath = "bin/Desk.exe", LaunchArguments = ["--e2e"] } };
        Assert.Equal(["--e2e"], WinAppTargetEngine.Create(target, Environment()).Options.App.LaunchArguments);
    }

    [Fact]
    public void The_environment_names_the_executable_over_the_config()
    {
        var target = new TargetConfig { Name = "desk", Platform = "windows", App = new AppConfig { BundleId = "Desk" } };
        var engine = WinAppTargetEngine.Create(target, Environment((WinAppTargetEngine.AppPathVariable, "out/Desk.exe")));
        Assert.Equal("out/Desk.exe", engine.Options.App.AppPath);
        Assert.Null(WinAppTargetEngine.Create(target, Environment((WinAppTargetEngine.AppPathVariable, ""))).Options.App.AppPath);
    }

    [Fact]
    public void Refuses_another_platform_a_url_and_a_target_without_an_app()
    {
        var ios = Assert.Throws<ConfigurationException>(() => WinAppTargetEngine.Create(new TargetConfig { Name = "t", Platform = "ios", App = new AppConfig { BundleId = "x" } }, Environment()));
        Assert.Contains("not windows", ios.Message, StringComparison.Ordinal);
        var url = Assert.Throws<ConfigurationException>(() => WinAppTargetEngine.Create(new TargetConfig { Name = "t", Platform = "windows", App = new AppConfig { Url = "https://x.test", BundleId = "x" } }, Environment()));
        Assert.Contains("app.url", url.Message, StringComparison.Ordinal);
        var none = Assert.Throws<ConfigurationException>(() => WinAppTargetEngine.Create(new TargetConfig { Name = "t", Platform = "windows", App = new AppConfig() }, Environment()));
        Assert.Equal("INVALID_CONFIG", none.Code);
        Assert.Contains("needs app.appPath or app.bundleId", none.Message, StringComparison.Ordinal);
    }
}
