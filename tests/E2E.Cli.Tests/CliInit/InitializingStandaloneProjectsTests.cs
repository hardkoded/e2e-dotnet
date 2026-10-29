// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Cli.Tests.CliInit;

/// <summary>
/// Upstream's <c>describe('initializing standalone projects')</c> of <c>tests/integration/cli-init.test.ts</c>. Only its
/// first <c>it</c> has a port. The others load the generated TypeScript scaffold and run <c>e2e run</c> or <c>e2e list</c> on
/// it (the engine-less HTTP example, the web and mobile scaffold as two targets, the generated browser example, the
/// <c>type</c> of the package.json, <c>.ts</c> and <c>.js</c> test collection, a typeless workspace package, the missing
/// config message of <c>e2e run</c>, and the look-alike test files), and the port has no runner, no TypeScript, and no
/// <c>e2e list</c>. The generated config is loaded by <see cref="E2eInitTests.Partial_Adds_the_runner_Playwright_and_AI_with_yes_and_does_not_install_or_prompt"/>.
/// </summary>
public sealed class InitializingStandaloneProjectsTests : CliInitTestBase
{
    [Fact]
    // Partial: the process is the in-process command, not a spawned CLI, and the directory is an argument, not the working directory.
    public async Task Partial_Preserves_configured_MCP_server_settings_when_reinitializing_a_project()
    {
        var original = new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                ["e2e"] = new JsonObject
                {
                    ["command"] = "e2e",
                    ["args"] = new JsonArray("mcp"),
                    ["env"] = new JsonObject { ["APP_ENV"] = "staging" },
                    ["timeout"] = 30_000,
                },
                ["other"] = new JsonObject { ["command"] = "other" },
            },
            ["clientSetting"] = true,
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true, IndentSize = 4, NewLine = "\n" }) + "\n";
        await File.WriteAllTextAsync(Path_(".mcp.json"), original);
        var code = await Program.RunAsync(["init", Dir, "--yes"], new StringWriter(), new StringWriter(), new StringReader(""), interactive: false, CancellationToken.None);
        Assert.Equal(0, code);
        Assert.Equal(original, Read(".mcp.json"));
    }
}
