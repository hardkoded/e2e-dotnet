// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace E2E.Cli.Tests.CliInit;

/// <summary>
/// Upstream's <c>describe('initializing standalone projects')</c> of <c>tests/integration/cli-init.test.ts</c>. Only its
/// first two <c>it</c>s have a port. The others load the generated TypeScript scaffold and run <c>e2e run</c> or <c>e2e list</c> on
/// it (the engine-less HTTP example, the web and mobile scaffold as two targets, the generated browser example, the
/// <c>type</c> of the package.json, <c>.ts</c> and <c>.js</c> test collection, a typeless workspace package, the missing
/// config message of <c>e2e run</c>, and the look-alike test files), and the port has no runner, no TypeScript, and no
/// <c>e2e list</c>. The generated config is loaded by <see cref="E2eInitTests.Partial_Adds_the_runner_Playwright_and_AI_with_yes_and_does_not_install_or_prompt"/>.
/// </summary>
public sealed class InitializingStandaloneProjectsTests : CliInitTestBase
{
    [Fact]
    // Partial: the port has no npm and no npm cache. The installed e2e tool is stood in for by a shim on the PATH, as upstream links a .bin shim, and the server starts without network access.
    public async Task Partial_Starts_the_installed_MCP_server_from_the_generated_Codex_config_with_an_empty_npm_cache_offline()
    {
        var code = await Program.RunAsync(["init", Dir, "--yes", "--mcp", ".codex/config.toml"], new StringWriter(), new StringWriter(), new StringReader(""), interactive: false, CancellationToken.None);
        Assert.Equal(0, code);
        // Linking the package alone does not create the executable an install provides.
        var bin = Directory.CreateDirectory(Path_("tool-bin")).FullName;
        var cli = Path.Combine(AppContext.BaseDirectory, "E2E.Cli.dll");
        if (OperatingSystem.IsWindows())
        {
            await File.WriteAllTextAsync(Path.Combine(bin, "e2e.cmd"), "@dotnet \"" + cli + "\" %*" + Environment.NewLine);
        }
        else
        {
            var shim = Path.Combine(bin, "e2e");
            await File.WriteAllTextAsync(shim, $"#!/bin/sh{'\n'}exec dotnet \"{cli}\" \"$@\"{'\n'}");
            File.SetUnixFileMode(shim, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var server = CodexConfig.ReadCodexConfig(Read(".codex/config.toml"))["mcp_servers"]!["e2e"]!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{ "command": "e2e", "args": ["mcp"] }"""), server));
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "init-codex-test",
            Command = (string)server["command"]!,
            Arguments = [.. server["args"]!.AsArray().Select(arg => (string)arg!)],
            WorkingDirectory = Dir,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
                ["CI"] = "",
                ["HTTP_PROXY"] = "http://127.0.0.1:9",
                ["HTTPS_PROXY"] = "http://127.0.0.1:9",
            },
        });
        await using var client = await McpClient.CreateAsync(transport, new McpClientOptions { ClientInfo = new Implementation { Name = "init-codex-test", Version = "1.0.0" } });
        Assert.Contains("open_session", (await client.ListToolsAsync()).Select(tool => tool.Name));
    }

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
