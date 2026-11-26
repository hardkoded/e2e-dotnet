// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.BrowserProvider;

namespace E2E.Tests.TargetApp;

/// <summary>The app under test is the target's: <c>web()</c> checks the target's <c>app</c> for what a browser needs.</summary>
public sealed class WebAndTheTargetAppTests
{
    [Fact]
    public void Needs_app_url_on_the_target_and_refuses_the_fields_of_an_installed_device_app()
    {
        var web = new WebEngine();
        const string name = "chromium";
        ValidateWeb(web, new AppConfig { Url = "http://localhost:3000" }, name);
        var missing = Assert.Throws<ConfigurationException>(() => ValidateWeb(web, new AppConfig(), name));
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
            var error = Assert.Throws<ConfigurationException>(() => ValidateWeb(web, app, name));
            Assert.Equal(
                $"target \"chromium\" declares app.{key}, which describes an installed device app; a web() target opens app.url",
                error.Message
            );
        }
    }

    [Fact]
    public void Declares_that_it_opens_a_file_app_in_a_browser_it_launches_itself_and_with_no_credentials_to_scope()
    {
        // The harness reads the URL and says so; the engine never parses the scheme itself.
        var info = new EngineAppCheckInfo { TargetName = "web", FileApp = true };
        var app = new EngineAppDeclaration { Url = "file:///tmp/site/index.html" };
        static ConfigurationException Refused(IEngine engine, EngineAppDeclaration app, EngineAppCheckInfo info)
        {
            var error = Assert.Throws<ConfigurationException>(() => engine.ValidateApp(app, info));
            Assert.Equal("INVALID_CONFIG", error.Code);
            return error;
        }

        foreach (var browser in new[] { BrowserName.Chromium, BrowserName.Firefox, BrowserName.Webkit })
        {
            var engine = new WebEngine(new WebEngineOptions { BrowserName = browser });
            Assert.True(((IEngine)engine).FileApps);
            ((IEngine)engine).ValidateApp(app, info);
        }

        var hosted = new InlineProvider("hosted", _ =>
            Task.FromResult(new BrowserLease { Id = "s", CdpEndpoint = "ws://localhost:9222" })
        );
        Assert.Contains(
            "target \"web\" opens a file app.url, which browser provider \"hosted\" cannot read",
            Refused(new WebEngine(new WebEngineOptions { Browser = hosted }), app, info).Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "web({ connect }) attaches to a browser that may run on another machine",
            Refused(
                new WebEngine(
                    new WebEngineOptions
                    {
                        Connect = new WebConnectOptions
                        {
                            CdpEndpoint = _ => Task.FromResult(new CdpTarget { Endpoint = "ws://localhost:9222" }),
                        },
                    }
                ),
                app,
                info
            ).Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "web({ headers }) always goes to the app's origin, which a file does not have",
            Refused(
                new WebEngine(
                    new WebEngineOptions { Headers = new Dictionary<string, string> { ["x-preview"] = "synthetic" } }
                ),
                app,
                info
            ).Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "web({ basicAuth }) always goes to the app's origin, which a file does not have",
            Refused(
                new WebEngine(new WebEngineOptions { BasicAuth = new WebBasicAuth("user", "synthetic") }),
                app,
                info
            ).Message,
            StringComparison.Ordinal
        );
        // A host named file is not a file app: the harness says nothing, and the options stand.
        (
            (IEngine)
                new WebEngine(
                    new WebEngineOptions
                    {
                        Browser = hosted,
                        Headers = new Dictionary<string, string> { ["x-preview"] = "synthetic" },
                    }
                )
        ).ValidateApp(
            new EngineAppDeclaration { Url = "file:8443/report" },
            new EngineAppCheckInfo { TargetName = "web" }
        );
    }

    [Fact]
    public void Takes_the_permissions_its_browser_grants_and_grants_only()
    {
        const string url = "http://localhost:3000";
        static AppConfig Of(string name, AppPermissionState state) =>
            new()
            {
                Url = url,
                Permissions = new Dictionary<string, AppPermissionState> { [name] = state },
            };
        static ConfigurationException Refused(WebEngine engine, AppConfig app)
        {
            var error = Assert.Throws<ConfigurationException>(() => ValidateWeb(engine, app, "web"));
            Assert.Equal("INVALID_CONFIG", error.Code);
            return error;
        }

        ValidateWeb(
            new WebEngine(),
            new AppConfig
            {
                Url = url,
                Permissions = new Dictionary<string, AppPermissionState>
                {
                    ["clipboard-read"] = AppPermissionState.Grant,
                    ["clipboard-write"] = AppPermissionState.Grant,
                },
            },
            "web"
        );
        ValidateWeb(
            new WebEngine(new WebEngineOptions { BrowserName = BrowserName.Webkit }),
            Of("clipboard-read", AppPermissionState.Grant),
            "web"
        );
        Assert.Contains(
            "target \"web\" app.permissions on firefox has unknown key \"clipboard-read\"",
            Refused(
                new WebEngine(new WebEngineOptions { BrowserName = BrowserName.Firefox }),
                Of("clipboard-read", AppPermissionState.Grant)
            ).Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "did you mean \"clipboard-write\"",
            Refused(new WebEngine(), Of("clipboard-wirte", AppPermissionState.Grant)).Message,
            StringComparison.Ordinal
        );
        foreach (var state in new[] { AppPermissionState.Deny, AppPermissionState.Reset })
        {
            Assert.Equal(
                $"target \"web\" app.permissions.camera is {state.ToString().ToLowerInvariant()}, but a web() target only grants; leave the permission out instead",
                Refused(new WebEngine(), Of("camera", state)).Message
            );
        }
    }

    private static void ValidateWeb(WebEngine engine, AppConfig app, string targetName) =>
        ((IEngine)engine).ValidateApp(
            TargetApps.EngineAppDeclarationOf(app),
            new EngineAppCheckInfo { TargetName = targetName }
        );
}
