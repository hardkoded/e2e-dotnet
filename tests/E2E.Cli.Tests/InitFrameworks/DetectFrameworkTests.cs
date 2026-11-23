// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitFrameworks;

/// <summary>Upstream's <c>describe('detectFramework')</c> of <c>tests/unit/init-frameworks.test.ts</c>.</summary>
public sealed class DetectFrameworkTests
{
    /// <summary>The scripts <c>create-next-app</c> writes.</summary>
    private static readonly Dictionary<string, string> NextScripts = new()
    {
        ["dev"] = "next dev",
        ["build"] = "next build",
        ["start"] = "next start",
    };

    private static Dictionary<string, string> Deps(string name, string version) => new() { [name] = version };

    private static FrameworkManifest NextWith(Dictionary<string, string> scripts) =>
        new(Dependencies: Deps("next", "*"), Scripts: scripts);

    [Fact]
    public void Finds_Next_js_in_dependencies_or_devDependencies_and_nothing_without_it()
    {
        var app = new FrameworkApp(
            "Next.js",
            "/frameworks/nextjs",
            "http://localhost:3000",
            "dev",
            new ProductionScriptNames("build", "start")
        );
        Assert.Equal(app, Frameworks.DetectFramework(new(Dependencies: Deps("next", "16.4.6"), Scripts: NextScripts)));
        Assert.Equal(app, Frameworks.DetectFramework(new(DevDependencies: Deps("next", "^16"), Scripts: NextScripts)));
        Assert.Null(Frameworks.DetectFramework(new(Dependencies: Deps("react", "^19"), Scripts: NextScripts)));
        Assert.Null(Frameworks.DetectFramework(new()));
    }

    [Theory]
    [InlineData("next dev --port 3100", 3100)]
    [InlineData("next dev --port=3100", 3100)]
    [InlineData("next dev -p 3100", 3100)]
    [InlineData("next dev -p3100", 3100)]
    [InlineData("next dev --turbopack -p 4000", 4000)]
    [InlineData("PORT=4100 next dev", 4100)]
    [InlineData("PORT=4100 next dev --port 4200", 4200)]
    [InlineData("concurrently \"next dev -p 4300\" \"tsc --watch\"", 4300)]
    [InlineData("next dev -p 4400 && echo ready", 4400)]
    [InlineData("node scripts/preflight.js && next dev --port 4500", 4500)]
    [InlineData("next dev", 3000)]
    [InlineData("turbo run dev", 3000)]
    public void Reads_the_port_of_the_dev_script(string dev, int port)
    {
        var found = Assert.IsType<FrameworkApp>(Frameworks.DetectFramework(NextWith(new() { ["dev"] = dev })));
        Assert.Equal($"http://localhost:{port}", found.Url);
    }

    [Theory]
    [InlineData("next dev -p $PORT")]
    [InlineData("next dev --port 0")]
    [InlineData("next dev --port 70000")]
    [InlineData("next dev --port")]
    [InlineData("node scripts/preflight.js -p 8080 && next dev")]
    [InlineData("export PORT=4000 && next dev")]
    public void Cannot_start_an_app_whose_dev_script_has_no_port_it_can_read(string dev)
    {
        Assert.Equal(
            new UnsupportedFramework(
                "Next.js",
                "/frameworks/nextjs",
                "init cannot read a fixed port from the dev script"
            ),
            Frameworks.DetectFramework(NextWith(new() { ["dev"] = dev }))
        );
    }

    [Fact]
    public void Cannot_start_an_app_with_no_dev_script()
    {
        var found = Assert.IsType<UnsupportedFramework>(
            Frameworks.DetectFramework(NextWith(new() { ["build"] = "next build" }))
        );
        Assert.Equal("package.json has no dev script", found.Reason);
    }

    [Fact]
    public void Tests_the_production_build_only_when_build_and_start_scripts_serve_it_on_the_dev_port()
    {
        static ProductionScriptNames? Production(Dictionary<string, string> scripts) =>
            ((FrameworkApp)Frameworks.DetectFramework(NextWith(scripts))!).Production;
        Assert.Equal(
            new ProductionScriptNames("build", "start"),
            Production(
                new()
                {
                    ["dev"] = "next dev -p 3100",
                    ["build"] = "next build",
                    ["start"] = "next start -p 3100",
                }
            )
        );
        Assert.Null(
            Production(
                new()
                {
                    ["dev"] = "next dev -p 3100",
                    ["build"] = "next build",
                    ["start"] = "next start",
                }
            )
        );
        Assert.Null(
            Production(
                new()
                {
                    ["dev"] = "next dev",
                    ["build"] = "next build",
                    ["start"] = "next start -p $PORT",
                }
            )
        );
        Assert.Null(Production(new() { ["dev"] = "next dev", ["start"] = "next start" }));
        Assert.Null(Production(new() { ["dev"] = "next dev", ["build"] = "next build" }));
    }

    [Fact]
    public void Finds_the_production_scripts_of_an_existing_app_on_any_port()
    {
        var expected = new ProductionScriptNames("build", "start");
        Assert.Equal(
            expected,
            Frameworks.ProductionScripts(
                NextWith(
                    new()
                    {
                        ["dev"] = "next dev -p 3100",
                        ["build"] = "next build",
                        ["start"] = "next start",
                    }
                )
            )
        );
        Assert.Equal(
            expected,
            Frameworks.ProductionScripts(NextWith(new() { ["build"] = "next build", ["start"] = "next start" }))
        );
        Assert.Null(Frameworks.ProductionScripts(NextWith(new() { ["start"] = "next start" })));
        Assert.Null(Frameworks.ProductionScripts(new(Scripts: NextScripts)));
    }
}
