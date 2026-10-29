// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.Config;

namespace E2E.Tests.AppPorts;

/// <summary><c>describe('an app URL on a free port')</c> of <c>app-ports.test.ts</c>.</summary>
public sealed class AnAppUrlOnAFreePortTests
{
    private const string Pnpm = """{ "executable": "pnpm", "args": ["dev"] }""";

    [Fact]
    public void Keeps_the_declared_zero_URL_and_asks_for_a_port_until_one_is_assigned()
    {
        var app = ResolveConfigTests.Resolved($$"""{ "url": "http://127.0.0.1:0", "command": {{Pnpm}} }""");
        Assert.Equal("http://127.0.0.1:0/", app.Base!.Href);
        Assert.Equal(new PortRequest("127.0.0.1", null), app.PortRequest);
        Assert.Equal("127.0.0.1", app.Site);
        Assert.Equal("http://127.0.0.1:0/", app.ReadyUrl);
        Assert.Equal("http://127.0.0.1:0/", app.Identity);
        Assert.Null(ResolveConfigTests.Resolved("""{ "url": "http://localhost:3000" }""").PortRequest);
        Assert.Null(ResolveConfigTests.Resolved("{}").PortRequest);
    }

    /// <summary>Partial: the port has no config digest to compare before and after the assignment, so the identity is compared and the digest is not.</summary>
    [Fact]
    public void Partial_Substitutes_the_assigned_port_in_the_base_URL_and_the_default_readyUrl_keeping_the_identity()
    {
        var pending = ResolveConfigTests.Resolved($$"""{ "url": "http://127.0.0.1:0/shop/", "command": {{Pnpm}} }""");
        var app = ResolveConfigTests.Resolved($$"""{ "url": "http://127.0.0.1:0/shop/", "command": {{Pnpm}} }""", port: 4321);
        Assert.Equal(pending.Identity, app.Identity);
        Assert.Equal("http://127.0.0.1:0/shop/", app.Identity);
        Assert.Equal(new E2E.Internal.NormalizedBaseUrl("http://127.0.0.1:4321/shop/", "http://127.0.0.1:4321", "/shop/"), app.Base);
        Assert.Equal(new PortRequest("127.0.0.1", 4321), app.PortRequest);
        Assert.Equal("http://127.0.0.1:4321/shop/", app.ReadyUrl);

        // A target that named its port ignores an assignment.
        Assert.Equal("http://localhost:3000", ResolveConfigTests.Resolved("""{ "url": "http://localhost:3000" }""", port: 4321).Base!.Origin);
    }

    [Fact]
    public void Expands_the_port_token_in_the_command_and_its_readiness_URL_to_the_app_port()
    {
        var app = ResolveConfigTests.Resolved(
            """
            { "url": "http://127.0.0.1:0", "readyUrl": "http://127.0.0.1:{port}/health",
              "command": { "executable": "pnpm", "args": ["dev", "--port", "{port}"],
                           "env": { "PORT": "{port}", "ORIGIN": "http://127.0.0.1:{port}", "NODE_ENV": "test" } } }
            """,
            port: 4321);
        Assert.Equal("pnpm", app.Command!.Executable);
        Assert.Equal(["dev", "--port", "4321"], app.Command.Args);
        Assert.Equal(
            new Dictionary<string, string> { ["PORT"] = "4321", ["ORIGIN"] = "http://127.0.0.1:4321", ["NODE_ENV"] = "test" }.OrderBy(pair => pair.Key),
            app.Command.Env!.OrderBy(pair => pair.Key));
        Assert.Equal("http://127.0.0.1:4321/health", app.ReadyUrl);
    }

    [Fact]
    public void Expands_the_port_token_to_the_fixed_or_default_port_of_app_url_and_refuses_it_without_one()
    {
        Assert.Equal("3000", ResolveConfigTests.Resolved("""{ "url": "http://localhost:3000", "command": { "executable": "pnpm", "env": { "PORT": "{port}" } } }""").Command!.Env!["PORT"]);
        Assert.Equal(["443"], ResolveConfigTests.Resolved("""{ "url": "https://app.test", "command": { "executable": "pnpm", "args": ["{port}"] } }""").Command!.Args);
        var args = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.App(
            """{ "command": { "executable": "npx", "args": ["expo", "start", "--port", "{port}"] }, "readyUrl": "http://127.0.0.1:8081/status" }"""));
        Assert.Equal("target \"web\" app.command.args uses {port}, but the target declares no url to take the port from", args.Message);
        var ready = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.App("""{ "command": { "executable": "node" }, "readyUrl": "http://127.0.0.1:{port}/" }"""));
        Assert.Equal("target \"web\" app.readyUrl uses {port}, but the target declares no url to take the port from", ready.Message);
    }

    [Fact]
    public void Refuses_a_free_port_nothing_starts_on_and_reuseExisting_on_one()
    {
        var nothing = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.App("""{ "url": "http://127.0.0.1:0" }"""));
        Assert.Equal("INVALID_CONFIG", nothing.Code);
        Assert.Equal("target \"web\" app.url asks for a free port (port 0), but nothing starts on it: add app.command to start the app there on {port}", nothing.Message);
        var reuse = Assert.Throws<ConfigurationException>(() => ResolveConfigTests.App("""{ "url": "http://127.0.0.1:0", "command": { "executable": "pnpm", "reuseExisting": true } }"""));
        Assert.Equal(
            "target \"web\" app.command.reuseExisting cannot find an app already running on a free port: port 0 is a new port every run; give the app a fixed port, or drop reuseExisting",
            reuse.Message);
    }
}
