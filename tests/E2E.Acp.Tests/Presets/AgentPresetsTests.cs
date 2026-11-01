// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using static E2E.Acp.Tests.AcpKit;

namespace E2E.Acp.Tests.Presets;

/// <summary><c>presets.test.ts</c>: <c>describe('agent presets')</c>.</summary>
public sealed class AgentPresetsTests : IDisposable
{
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
                // The agent may still hold its log open.
            }
        }
    }

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
            File.WriteAllText(Path.Combine(package, "package.json"), JsonSerializer.Serialize(new { name, type = "module", bin = new Dictionary<string, string> { [bin] = "dist/index.js" } }));
            File.WriteAllText(Path.Combine(package, "dist", "index.js"), "import " + JsonSerializer.Serialize(new Uri(AcpKit.Agent).AbsoluteUri) + ";\n");
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
    private static async Task<List<JsonObject>> RunStepAsync(IStepExecutor executor, string log)
    {
        await using var session = await StartAsync(World(), executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("add a todo");
        return [.. File.ReadAllLines(log).Where(line => line.Length > 0).Select(line => (JsonObject)JsonNode.Parse(line)!)];
    }

    [Fact]
    public void Refuses_a_preset_whose_adapter_the_project_has_not_installed()
    {
        var directory = Project();
        var claude = Assert.Throws<ConfigurationException>(() => InProject(directory, () => AcpExecutors.ClaudeCode()));
        Assert.Equal("INVALID_CONFIG", claude.Code);
        Assert.Contains("npm install -D @agentclientprotocol/claude-agent-acp", claude.Message);
        var codex = Assert.Throws<ConfigurationException>(() => InProject(directory, () => AcpExecutors.Codex()));
        Assert.Contains("npm install -D @agentclientprotocol/codex-acp", codex.Message);
    }

    [Fact]
    public async Task Starts_Claude_Codes_installed_adapter_with_its_own_tools_settings_and_saved_sessions_off()
    {
        var directory = Project("@agentclientprotocol/claude-agent-acp");
        var log = Path.Combine(directory, "agent.jsonl");
        var executor = InProject(directory, () => AcpExecutors.ClaudeCode(new AcpAgentOptions
        {
            Model = "slow",
            Env = new Dictionary<string, string> { ["ACP_SCRIPT"] = JsonSerializer.Serialize(new[] { new[] { Pass } }), ["ACP_LOG"] = log },
        }));

        Assert.Equal("claude-code", executor.Name);
        var entries = await RunStepAsync(executor, log);
        var meta = entries.First(entry => entry.ContainsKey("session"))["session"]!["meta"]!;
        Assert.Equal(
            JsonNode.Parse("""{"claudeCode":{"options":{"tools":[],"settingSources":[],"strictMcpConfig":true,"persistSession":false,"allowDangerouslySkipPermissions":false}}}""")!.ToJsonString(),
            meta.ToJsonString());
        Assert.Contains(entries, entry => entry["config"]?["model"]?.GetValue<string>() == "slow");
    }

    [Fact]
    public async Task Starts_Codex_read_only_with_its_own_tools_and_the_users_MCP_servers_off()
    {
        if (OperatingSystem.IsWindows())
        {
            // The stand-in Codex is a shell script.
            return;
        }

        var directory = Project("@agentclientprotocol/codex-acp");
        var log = Path.Combine(directory, "agent.jsonl");
        // Stands in for the Codex the adapter runs, which lists the user's MCP servers, and none when it gets a secret.
        var codexBin = Path.Combine(directory, "codex");
        File.WriteAllText(codexBin, "#!/bin/sh\n[ \"$1 $2 $3\" = \"mcp list --json\" ] && [ -z \"$E2E_SECRET_KEY\" ] && echo '[{\"name\":\"playwright\"},{\"name\":\"executor\"}]'\n");
        File.SetUnixFileMode(codexBin, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var executor = InProject(directory, () => AcpExecutors.Codex(new AcpAgentOptions
        {
            Env = new Dictionary<string, string>
            {
                ["CODEX_PATH"] = codexBin,
                ["E2E_SECRET_KEY"] = "sk_test_value",
                ["ACP_SCRIPT"] = JsonSerializer.Serialize(new[] { new[] { Pass } }),
                ["ACP_LOG"] = log,
            },
        }));

        var entries = await RunStepAsync(executor, log);
        Assert.Contains(entries, entry => entry["mode"]?.GetValue<string>() == "read-only");
        var config = JsonNode.Parse(entries.First(entry => entry.ContainsKey("codexConfig"))["codexConfig"]!.GetValue<string>())!;
        Assert.Equal("disabled", config["web_search"]!.GetValue<string>());
        foreach (var feature in new[] { "shell_tool", "multi_agent", "plugins", "apps", "browser_use" })
        {
            Assert.False(config["features"]![feature]!.GetValue<bool>());
        }

        Assert.Equal("""{"playwright":{"enabled":false},"executor":{"enabled":false}}""", config["mcp_servers"]!.ToJsonString());
    }
}
