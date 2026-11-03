// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Acp.Tests.Executor;
using static E2E.Acp.Tests.Executor.AcpHarness;

namespace E2E.Acp.Tests.Presets;

/// <summary>
/// Ports <c>describe('agent presets')</c> of tester-army/e2e <c>packages/acp/tests/presets.test.ts</c> (0410fc9c). Upstream installs its scripted
/// agent under an adapter's package name; here the adapter is a one-line Node script that starts the scripted agent, since the presets run an
/// adapter with <c>node</c>. The tests change the working directory, as a config loaded from a project does.
/// </summary>
[Collection(WorkingDirectoryCollection.Name)]
public sealed class AgentPresetsTests : IDisposable
{
    private static readonly object[] Pass = [new { call = "done", args = new { status = "passed", summary = "added" } }];

    private readonly List<string> _directories = [];

    public void Dispose()
    {
        foreach (var directory in _directories)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // The scripted agent may still hold its log open.
            }
        }
    }

    [Fact]
    public void Refuses_a_preset_whose_adapter_the_project_has_not_installed()
    {
        var directory = Project();

        var claude = Assert.Throws<ConfigurationException>(() => InProject(directory, () => AcpExecutor.ClaudeCode()));
        var codex = Assert.Throws<ConfigurationException>(() => InProject(directory, () => AcpExecutor.Codex()));

        Assert.Equal("INVALID_CONFIG", claude.Code);
        Assert.Contains("npm install -D @agentclientprotocol/claude-agent-acp", claude.Message, StringComparison.Ordinal);
        Assert.Contains("npm install -D @agentclientprotocol/codex-acp", codex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Starts_Claude_Codes_installed_adapter_with_its_own_tools_settings_and_saved_sessions_off()
    {
        var directory = Project("@agentclientprotocol/claude-agent-acp");
        var log = Path.Combine(directory, "agent.jsonl");
        var executor = InProject(directory, () => AcpExecutor.ClaudeCode(new AcpAgentOptions { Model = "slow", Env = Script(log) }));
        Assert.Equal("claude-code", executor.Name);

        var entries = await RunStepAsync(executor, new ScriptedAgent(executor, log));

        var session = Assert.Single(entries, entry => entry.ContainsKey("session"))["session"]!;
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{"claudeCode":{"options":{"tools":[],"settingSources":[],"strictMcpConfig":true,"persistSession":false,"allowDangerouslySkipPermissions":false}}}"""),
            session["meta"]));
        Assert.Contains(entries, entry => entry["config"]?["model"]?.GetValue<string>() == "slow");
    }

    [Fact]
    public async Task Starts_Codex_read_only_with_its_own_tools_and_the_users_MCP_servers_off()
    {
        var directory = Project("@agentclientprotocol/codex-acp");
        var log = Path.Combine(directory, "agent.jsonl");

        // Stands in for the Codex the adapter runs, which lists the user's MCP servers, and none when it gets a secret.
        var codexBin = OperatingSystem.IsWindows() ? Path.Combine(directory, "codex.cmd") : Path.Combine(directory, "codex");
        const string servers = """[{"name":"playwright"},{"name":"executor"}]""";
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(codexBin, $"@echo off\r\nif \"%1 %2 %3\"==\"mcp list --json\" if \"%E2E_SECRET_KEY%\"==\"\" echo {servers}\r\n");
        }
        else
        {
            File.WriteAllText(codexBin, $"#!/bin/sh\n[ \"$1 $2 $3\" = \"mcp list --json\" ] && [ -z \"$E2E_SECRET_KEY\" ] && echo '{servers}'\n");
            File.SetUnixFileMode(codexBin, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var env = Script(log);
        env["CODEX_PATH"] = codexBin;
        env["E2E_SECRET_KEY"] = "sk_test_value";
        var executor = InProject(directory, () => AcpExecutor.Codex(new AcpAgentOptions { Env = env }));

        var entries = await RunStepAsync(executor, new ScriptedAgent(executor, log));

        Assert.Contains(entries, entry => entry["mode"]?.GetValue<string>() == "read-only");
        var config = JsonNode.Parse(Assert.Single(entries, entry => entry.ContainsKey("codexConfig"))["codexConfig"]!.GetValue<string>())!;
        Assert.Equal("disabled", config["web_search"]!.GetValue<string>());
        foreach (var feature in new[] { "shell_tool", "multi_agent", "plugins", "apps", "browser_use" })
        {
            Assert.False(config["features"]![feature]!.GetValue<bool>(), feature);
        }

        var mcpServers = config["mcp_servers"]!.AsObject();
        Assert.Equal(["playwright", "executor"], mcpServers.Select(server => server.Key));
        Assert.All(mcpServers, server => Assert.False(server.Value!["enabled"]!.GetValue<bool>()));
    }

    /// <summary>The scripted agent's environment: one passing turn, logged to <paramref name="log"/>.</summary>
    private static Dictionary<string, string> Script(string log) => new() { ["ACP_SCRIPT"] = JsonSerializer.Serialize(new[] { Pass }), ["ACP_LOG"] = log };

    /// <summary>A project with the scripted agent installed under an adapter's package name.</summary>
    private string Project(params string[] adapters)
    {
        var directory = Directory.CreateTempSubdirectory("e2e-acp-preset-").FullName;
        _directories.Add(directory);
        foreach (var name in adapters)
        {
            var package = Path.Combine(directory, "node_modules", name);
            Directory.CreateDirectory(Path.Combine(package, "dist"));
            var bin = name.Split('/')[^1];
            File.WriteAllText(Path.Combine(package, "package.json"), JsonSerializer.Serialize(new { name, bin = new Dictionary<string, string> { [bin] = "dist/index.js" } }));
            File.WriteAllText(
                Path.Combine(package, "dist", "index.js"),
                $"const {{ spawn }} = require('node:child_process');\nspawn({JsonSerializer.Serialize(AgentExecutable)}, [], {{ stdio: 'inherit' }}).on('exit', (code) => process.exit(code ?? 1));\n");
        }

        return directory;
    }

    /// <summary>Builds the executor from inside <paramref name="directory"/>, as a config loaded there does.</summary>
    private static T InProject<T>(string directory, Func<T> build)
    {
        var previous = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(directory);
        try
        {
            return build();
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }

    /// <summary>Runs one passing step and returns what the agent logged.</summary>
    private static async Task<IReadOnlyList<JsonObject>> RunStepAsync(IStepExecutor executor, ScriptedAgent agent)
    {
        await using var session = await StartAsync(executor, new TodoPage().World());

        await session.Agent.ActAsync("add a todo");

        return agent.Log();
    }
}
