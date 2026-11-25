// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Cli.Tests.InitCi;

/// <summary>Upstream's <c>describe('reading an existing config')</c>. The JSON config names its model by <c>provider</c>, where upstream's TypeScript config imports it.</summary>
public sealed class ReadingAnExistingConfigTests
{
    private static JsonObject Config(string json) => Ci.ReadConfig(json)!;

    [Fact]
    // Partial: the gateway is named by the provider id init writes, not by an import line.
    public void Partial_Names_the_gateways_by_the_provider_init_writes()
    {
        Assert.Equal(
            ["vercel"],
            Ci.GatewaysInConfig(Config("""{ "agents": { "default": { "provider": "gateway" } } }""")).Select(g => g.Id)
        );
        Assert.Equal(
            ["chatgpt"],
            Ci.GatewaysInConfig(Config("""{ "agents": { "default": { "provider": "chatgpt" } } }""")).Select(g => g.Id)
        );
        Assert.Empty(Ci.GatewaysInConfig(Config("""{ "agents": { "default": { "provider": "anthropic" } } }""")));
        // As a hand edit leaves it: comments and a trailing comma.
        Assert.Equal(
            ["openrouter"],
            Ci.GatewaysInConfig(
                    Config("{ // gateway\n \"agents\": { \"default\": { \"provider\": \"openrouter\", } } }")
                )
                .Select(g => g.Id)
        );
        Assert.Empty(Ci.GatewaysInConfig(Config("{}")));
    }

    [Fact]
    public void Partial_Finds_an_OpenAI_compatible_baseUrl_on_this_machine()
    {
        static bool Local(string url) =>
            Ci.ConfigUsesLocalModel(Config("{ \"agents\": { \"default\": { \"baseUrl\": \"" + url + "\" } } }"));
        Assert.True(Local("http://127.0.0.1:11434/v1"));
        Assert.True(Local("http://localhost:8080/v1"));
        Assert.False(Local("https://llm.example/v1"));
        Assert.False(Ci.ConfigUsesLocalModel(Config("""{ "agents": { "default": { } } }""")));
    }

    [Fact]
    // Partial: a command is a key of app, and a commented-out one is a comment in JSON.
    public void Partial_Tells_a_command_the_runner_starts_from_a_commented_out_one()
    {
        Assert.True(
            Ci.ConfigStartsApp(Config("""{ "app": { "command": { "executable": "dotnet", "args": ["run"] } } }"""))
        );
        Assert.True(
            Ci.ConfigStartsApp(Config("""{ "targets": [{ "app": { "command": { "executable": "dotnet" } } }] }"""))
        );
        Assert.False(Ci.ConfigStartsApp(Config("{ \"app\": {\n // \"command\": { \"executable\": \"dotnet\" }\n } }")));
    }

    [Fact]
    // Partial: a command is a key of app in a JSON config, where upstream reads the text of e2e.config.ts.
    public void Partial_Finds_a_package_json_script_the_config_runs_as_a_command_outside_comments()
    {
        Assert.True(
            Ci.ConfigRunsScript(
                Config("""{ "targets": [{ "app": { "command": { "executable": "npm", "args": ["run", "start"] } } }] }"""),
                "start"
            )
        );
        Assert.True(
            Ci.ConfigRunsScript(
                Config("""{ "app": { "command": { "executable": "pnpm", "args": [ "run", "start" ] } } }"""),
                "start"
            )
        );
        Assert.False(
            Ci.ConfigRunsScript(
                Config("""{ "app": { "command": { "executable": "npm", "args": ["run", "start:prod"] } } }"""),
                "start"
            )
        );
        Assert.False(
            Ci.ConfigRunsScript(
                Config("""{ "app": { "command": { "executable": "npm", "args": ["run", "dev"], "reuseExisting": true } } }"""),
                "start"
            )
        );
        Assert.False(
            Ci.ConfigRunsScript(
                Config("{ \"app\": {\n // \"command\": { \"executable\": \"npm\", \"args\": [\"run\", \"start\"] }\n } }"),
                "start"
            )
        );
    }
}
