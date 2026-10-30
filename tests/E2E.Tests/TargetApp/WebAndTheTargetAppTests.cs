// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.TargetApp;

/// <summary>The app under test is the target's: <c>web()</c> checks the target's <c>app</c> for what a browser needs.</summary>
public sealed class WebAndTheTargetAppTests
{
    [Fact]
    public void Needs_app_url_on_the_target_and_refuses_the_fields_of_an_installed_device_app()
    {
        var web = new WebEngine();
        const string name = "chromium";
        web.ValidateApp(new AppConfig { Url = "http://localhost:3000" }, name);
        var missing = Assert.Throws<ConfigurationException>(() => web.ValidateApp(new AppConfig(), name));
        Assert.Equal("INVALID_CONFIG", missing.Code);
        Assert.Contains("target \"chromium\" needs app.url", missing.Message, StringComparison.Ordinal);
        foreach (var key in new[] { "bundleId", "appPath", "launchArguments" })
        {
            var app = key switch
            {
                "bundleId" => new AppConfig { Url = "http://localhost:3000", BundleId = "x" },
                "appPath" => new AppConfig { Url = "http://localhost:3000", AppPath = "x" },
                _ => new AppConfig { Url = "http://localhost:3000", LaunchArguments = ["-x"] },
            };
            var error = Assert.Throws<ConfigurationException>(() => web.ValidateApp(app, name));
            Assert.Equal($"target \"chromium\" declares app.{key}, which describes an installed device app; a web() target opens app.url", error.Message);
        }
    }

    [Fact]
    public void Takes_the_permissions_its_browser_grants_and_grants_only()
    {
        const string url = "http://localhost:3000";
        static AppConfig Of(string name, AppPermissionState state) =>
            new() { Url = url, Permissions = new Dictionary<string, AppPermissionState> { [name] = state } };
        static ConfigurationException Refused(WebEngine engine, AppConfig app)
        {
            var error = Assert.Throws<ConfigurationException>(() => engine.ValidateApp(app, "web"));
            Assert.Equal("INVALID_CONFIG", error.Code);
            return error;
        }

        new WebEngine().ValidateApp(
            new AppConfig
            {
                Url = url,
                Permissions = new Dictionary<string, AppPermissionState>
                {
                    ["clipboard-read"] = AppPermissionState.Grant,
                    ["clipboard-write"] = AppPermissionState.Grant,
                },
            },
            "web");
        new WebEngine(new WebEngineOptions { BrowserName = BrowserName.Webkit }).ValidateApp(Of("clipboard-read", AppPermissionState.Grant), "web");
        Assert.Contains(
            "target \"web\" app.permissions on firefox has unknown key \"clipboard-read\"",
            Refused(new WebEngine(new WebEngineOptions { BrowserName = BrowserName.Firefox }), Of("clipboard-read", AppPermissionState.Grant)).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "did you mean \"clipboard-write\"",
            Refused(new WebEngine(), Of("clipboard-wirte", AppPermissionState.Grant)).Message,
            StringComparison.Ordinal);
        foreach (var state in new[] { AppPermissionState.Deny, AppPermissionState.Reset })
        {
            Assert.Equal(
                $"target \"web\" app.permissions.camera is {state.ToString().ToLowerInvariant()}, but a web() target only grants; leave the permission out instead",
                Refused(new WebEngine(), Of("camera", state)).Message);
        }
    }
}
