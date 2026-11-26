// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.FileAppConfig;

/// <summary>How a target resolves when its <c>app.url</c> is a file: no host, so no site, origin, port, or readiness poll (upstream's <c>describe('a file app.url')</c>).</summary>
public sealed class AFileAppUrlTests
{
    private const string Url = "file:///tmp/e2e-file-app/site/index.html";

    /// <summary>An engine that declares it opens a file app.</summary>
    private static readonly FilesEngine FileEngine = new("files", fileApps: true);

    /// <summary>The app a target named <c>web</c> resolves to from what its config declares.</summary>
    private static ResolvedApp Resolve(AppConfig declared)
    {
        TargetApps.CheckTargetApp(Target(declared), FileEngine);
        return TargetApps.ResolveTargetApp("web", declared, "/tmp/project");
    }

    private static TargetConfig Target(AppConfig app) =>
        new()
        {
            Name = "web",
            Platform = "web",
            App = app,
        };

    [Fact]
    public void Resolves_with_no_site_or_origin_as_a_test_environment_identified_by_the_file()
    {
        var app = Resolve(new AppConfig { Url = Url });
        Assert.Equal(Url, app.Base?.Href);
        Assert.Null(app.Site);
        Assert.Equal("test", app.Environment);
        Assert.Equal(Url, app.Identity);
        Assert.Null(app.PortRequest);
        Assert.Equal(new EngineAppInfo(), TargetApps.EngineAppInfoOf(app));
    }

    [Fact]
    public void Keeps_the_origin_and_site_of_an_http_app()
    {
        var app = Resolve(new AppConfig { Url = "https://app.example.com/shop" });
        Assert.Equal(
            new EngineAppInfo { Origin = "https://app.example.com", Site = "example.com" },
            TargetApps.EngineAppInfoOf(app)
        );
        Assert.Equal("https://app.example.com/shop", app.Identity);
    }

    [Fact]
    public void Needs_a_readyUrl_beside_a_command_since_a_file_answers_no_poll()
    {
        var command = new CommandConfig { Executable = "npm", Args = ["run", "api"] };
        var error = Assert.Throws<ConfigurationException>(() =>
            Resolve(new AppConfig { Url = Url, Command = command })
        );
        Assert.Equal("APP_URL_REQUIRED", error.Code);
        Assert.Contains(
            "a file answers no readiness poll: declare app.readyUrl",
            error.Message,
            StringComparison.Ordinal
        );
        Assert.Equal(
            "http://localhost:4000/health",
            Resolve(
                new AppConfig
                {
                    Url = Url,
                    Command = command,
                    ReadyUrl = "http://localhost:4000/health",
                }
            ).ReadyUrl
        );
    }

    [Fact]
    public void Is_refused_for_an_engine_that_does_not_declare_it_opens_one()
    {
        var plain = new FilesEngine("plain", fileApps: false);
        var error = Assert.Throws<ConfigurationException>(() =>
            TargetApps.CheckTargetApp(Target(new AppConfig { Url = Url }), plain)
        );
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains(
            "engine \"plain\" does not declare that it opens one from this machine's disk",
            error.Message,
            StringComparison.Ordinal
        );
        TargetApps.CheckTargetApp(Target(new AppConfig { Url = "https://app.example.com" }), plain);
    }

    [Fact]
    public void Tells_the_engine_it_is_a_file_app_by_the_harnesss_own_reading_of_the_URL()
    {
        var seen = new List<EngineAppCheckInfo>();
        var engine = new FilesEngine("files", fileApps: true, (_, info) => seen.Add(info));
        foreach (
            var url in new[]
            {
                Url,
                "FILE:/tmp/e2e-file-app/site/index.html",
                "https://app.example.com",
                // file:8443/report is the host file on port 8443, as it was before file apps.
                "file:8443/report",
            }
        )
        {
            TargetApps.CheckTargetApp(Target(new AppConfig { Url = url }), engine);
        }

        Assert.Equal(
            [
                new EngineAppCheckInfo { TargetName = "web", FileApp = true },
                new EngineAppCheckInfo { TargetName = "web", FileApp = true },
                new EngineAppCheckInfo { TargetName = "web" },
                new EngineAppCheckInfo { TargetName = "web" },
            ],
            seen
        );
    }

    [Fact]
    public void Keeps_a_host_named_file_an_https_app_with_the_web_engines_headers()
    {
        var web = new WebEngine(
            new WebEngineOptions { Headers = new Dictionary<string, string> { ["x-preview"] = "synthetic" } }
        );
        var declared = new AppConfig { Url = "file:8443/report" };
        TargetApps.CheckTargetApp(Target(declared), web);
        var app = TargetApps.ResolveTargetApp("web", declared, "/tmp/project");
        Assert.Equal("https://file:8443/report", app.Base?.Href);
        Assert.Equal("https://file:8443", TargetApps.EngineAppInfoOf(app).Origin);
    }

    [Fact]
    public void Refuses_port_in_a_command_having_no_port_to_give()
    {
        var error = Assert.Throws<ConfigurationException>(() =>
            Resolve(
                new AppConfig
                {
                    Url = Url,
                    ReadyUrl = "http://localhost:4000",
                    Command = new CommandConfig { Executable = "serve", Args = ["-p", "{port}"] },
                }
            )
        );
        Assert.Equal("INVALID_CONFIG", error.Code);
    }
}
