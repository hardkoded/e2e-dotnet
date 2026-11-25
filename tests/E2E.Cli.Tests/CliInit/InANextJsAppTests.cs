// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Cli.Tests.CliInit;

/// <summary>
/// Upstream's <c>describe('in a Next.js app')</c> of <c>tests/unit/cli-init.test.ts</c>. The port's config is JSON, which cannot
/// switch on <c>CI</c>, so a new config's target runs the dev script everywhere and its pipeline gets no build step; the
/// <c>it</c> "builds the app in the CI pipeline before the tests, and says nothing is missing to start it" is not ported
/// (issue: CI switch in the generated target). A pipeline for an existing config that runs the start script does build.
/// </summary>
public sealed class InANextJsAppTests : CliInitTestBase
{
    private const string DefaultScripts = """{ "dev": "next dev", "build": "next build", "start": "next start" }""";

    /// <summary>Writes the package.json <c>create-next-app</c> writes, with <paramref name="scripts"/> in place of its own, and the lockfile that names the manager.</summary>
    private void NextApp(string scripts = DefaultScripts, string? lockfile = null, string? packageManager = null)
    {
        var field = packageManager is null ? "" : $$"""  "packageManager": "{{packageManager}}",""";
        File.WriteAllText(
            Path_("package.json"),
            $$"""{ {{field}} "name": "my-app", "private": true, "scripts": {{scripts}}, "dependencies": { "next": "16.4.6", "react": "19.3.0", "react-dom": "19.3.0" } }"""
        );
        if (lockfile is not null)
        {
            File.WriteAllText(Path_(lockfile), "");
        }
    }

    private JsonObject WebApp() => (JsonObject)Json("e2e.config.json")["targets"]![0]!["app"]!;

    [Fact]
    // Partial: the JSON config cannot pick the production build in CI, so the dev script starts there too.
    public async Task Partial_Starts_next_dev_and_asks_for_no_APP_URL()
    {
        NextApp();
        Assert.Equal(0, (await RunInitAsync(yes: true)).ExitCode);
        var app = WebApp();
        Assert.Equal("http://localhost:3000", (string)app["url"]!);
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse("""{ "executable": "npm", "args": ["run", "dev"], "reuseExisting": true }"""),
                app["command"]
            )
        );
        Assert.DoesNotContain("APP_URL", Read("e2e.config.json"), StringComparison.Ordinal);
        Assert.Contains(
            "Found Next.js: the web target starts npm run dev on http://localhost:3000",
            Output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    // Partial: no production build is written.
    public async Task Partial_Runs_the_scripts_with_pnpm_on_the_port_the_dev_script_sets()
    {
        NextApp(
            """{ "dev": "next dev --turbopack -p 3100", "build": "next build", "start": "next start --port 3100" }""",
            packageManager: "pnpm@10.0.0"
        );
        Assert.Equal(0, (await RunInitAsync(yes: true)).ExitCode);
        var app = WebApp();
        Assert.Equal("http://localhost:3100", (string)app["url"]!);
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse("""{ "executable": "pnpm", "args": ["run", "dev"], "reuseExisting": true }"""),
                app["command"]
            )
        );
        Assert.Contains(
            "Found Next.js: the web target starts pnpm dev on http://localhost:3100",
            Output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    // Partial: the config text is JSON, so there is no `const ci` to leave out.
    public async Task Partial_Starts_next_dev_in_CI_too_when_start_serves_on_another_port()
    {
        NextApp("""{ "dev": "next dev -p 3100", "build": "next build", "start": "next start" }""");
        Assert.Equal(0, (await RunInitAsync(yes: true)).ExitCode);
        var app = WebApp();
        Assert.Equal("http://localhost:3100", (string)app["url"]!);
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse("""{ "executable": "npm", "args": ["run", "dev"], "reuseExisting": true }"""),
                app["command"]
            )
        );
        Assert.Contains(
            "Found Next.js: the web target starts npm run dev on http://localhost:3100\n",
            Output.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal
        );
    }

    [Fact]
    // Partial: the placeholder target is the default web target, which names no APP_URL.
    public async Task Partial_Keeps_the_placeholder_target_and_says_why_when_it_cannot_start_the_app()
    {
        NextApp("""{ "build": "next build", "start": "next start" }""");
        Assert.Equal(0, (await RunInitAsync(yes: true)).ExitCode);
        var app = WebApp();
        Assert.Equal("http://localhost:3000", (string)app["url"]!);
        Assert.Null(app["command"]);
        Assert.Contains(
            "Found Next.js, but package.json has no dev script; set app.url and app.command in e2e.config.json, see https://www.hardkoded.com/e2e-dotnet/frameworks/nextjs",
            Output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    // Partial: the platform of the mobile target is the host's.
    public async Task Partial_Leaves_a_config_with_no_web_engine_alone()
    {
        NextApp();
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Engines = ["mobile"] })).ExitCode);
        Assert.DoesNotContain("localhost", Read("e2e.config.json"), StringComparison.Ordinal);
        Assert.DoesNotContain("Found Next.js", Output, StringComparison.Ordinal);
    }

    private const string EngineProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
          <ItemGroup><PackageReference Include="E2E" Version="*" /><PackageReference Include="E2E.NUnit" Version="*" /></ItemGroup>
        </Project>
        """;

    private void ExistingConfig(string config, string scripts = DefaultScripts)
    {
        NextApp(scripts);
        File.WriteAllText(Path_(ProjectFile()), EngineProject);
        File.WriteAllText(Path_("e2e.config.json"), config);
    }

    [Fact]
    // Partial: the config is JSON and runs start everywhere.
    public async Task Partial_Builds_the_app_in_a_pipeline_added_later_when_the_existing_config_serves_the_start_script()
    {
        ExistingConfig(
            """{ "targets": [{ "platform": "web", "app": { "url": "http://localhost:3000", "command": { "executable": "npm", "args": ["run", "start"] } } }] }"""
        );
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Ci = "gitlab" })).ExitCode);
        Assert.Contains("    - npm run build\n", Read(".gitlab-ci.yml"), StringComparison.Ordinal);
        Assert.DoesNotContain("Found Next.js", Output, StringComparison.Ordinal);
    }

    [Fact]
    // Partial: the config is JSON, so it serves start on the one URL, where upstream's switches both on CI.
    public async Task Partial_Builds_the_app_for_an_existing_config_that_serves_start_on_another_port_than_dev()
    {
        ExistingConfig(
            """{ "targets": [{ "platform": "web", "app": { "url": "http://localhost:3000", "command": { "executable": "npm", "args": ["run", "start"] } } }] }""",
            """{ "dev": "next dev -p 3100", "build": "next build", "start": "next start" }"""
        );
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" })).ExitCode);
        Assert.Contains("      - name: Build the app\n        run: npm run build\n", Read(".github/workflows/e2e.yml"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Builds_nothing_for_an_existing_config_that_runs_only_next_dev()
    {
        ExistingConfig(
            """{ "targets": [{ "platform": "web", "app": { "url": "http://localhost:3000", "command": { "executable": "npm", "args": ["run", "dev"] } } }] }"""
        );
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Ci = "github-actions" })).ExitCode);
        Assert.DoesNotContain("Build the app", Read(".github/workflows/e2e.yml"), StringComparison.Ordinal);
    }
}
