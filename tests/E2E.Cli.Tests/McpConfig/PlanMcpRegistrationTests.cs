// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Internal;
using Config = global::E2E.Cli.McpConfig;

namespace E2E.Cli.Tests.McpConfig;

/// <summary>
/// Upstream's <c>describe('planMcpRegistration')</c> of <c>mcp-config.test.ts</c>. The port's server entry is <c>e2e mcp</c>
/// where upstream's is <c>npx e2e mcp</c>, so the stale entries below use <c>npx</c>. Upstream parses the TOML with
/// <c>toml-eslint-parser</c>; the port compares through <c>CodexConfig.ReadCodexConfig</c>.
/// </summary>
public sealed class PlanMcpRegistrationTests : IDisposable
{
    private static readonly bool Symlinks = ProbeSymlinks();

    private static readonly string[] CodexOnly = [".codex/config.toml"];

    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-mcp-config-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static JsonObject Entry() => new() { ["command"] = "e2e", ["args"] = new JsonArray("mcp") };

    private string P(string file) => Path.Combine(_dir, file);

    private void Write(string file, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(P(file))!);
        File.WriteAllText(P(file), text);
    }

    private static bool ProbeSymlinks()
    {
        var probe = Directory.CreateTempSubdirectory("e2e-mcp-symlink-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(probe, "file"), "");
            File.CreateSymbolicLink(Path.Combine(probe, "link"), Path.Combine(probe, "file"));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            Directory.Delete(probe, recursive: true);
        }
    }

    [Fact]
    public void Creates_each_missing_file_with_the_e2e_server_entry()
    {
        var plan = Config.PlanMcpRegistration(_dir, [.. Config.McpLocations.Select(location => location.File)]);
        Assert.Equal(
            [(".mcp.json", false), (".cursor/mcp.json", false), (".codex/config.toml", false)],
            plan.Registrations.Select(registration => (registration.Relative, registration.Existing)));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["mcpServers"] = new JsonObject { ["e2e"] = Entry() } }, JsonNode.Parse(plan.Registrations[0].Content)));
        Assert.EndsWith("\n", plan.Registrations[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Registers_Codex_in_its_project_TOML_config_and_discovers_it_on_the_next_run()
    {
        var plan = Config.PlanMcpRegistration(_dir, CodexOnly).Registrations[0];
        Assert.Contains("[mcp_servers.e2e]", plan.Content, StringComparison.Ordinal);
        Assert.Contains("command = \"e2e\"", plan.Content, StringComparison.Ordinal);
        Assert.Contains("\"mcp\"", plan.Content, StringComparison.Ordinal);
        Write(".codex/config.toml", plan.Content);
        Assert.Equal([".codex/config.toml"], Config.FindRegisteredMcpFiles(_dir));
        Assert.Empty(Config.PlanMcpRegistration(_dir, CodexOnly).Registrations);
    }

    [Theory]
    [InlineData("# My settings\nmodel = \"custom\"\n")]
    [InlineData("# My settings\n[mcp_servers.other]\ncommand = \"other\"\n")]
    [InlineData("# My settings\n[mcp_servers.\"e2e\"] # server\ncommand = \"npx\" # launch\nargs = [\n  \"e2e\",\n  \"mcp\",\n]\nenabled = false\n[mcp_servers.e2e.env]\nAPP_ENV = \"staging\"\n")]
    [InlineData("# My settings\n[mcp_servers]\ne2e.command = \"npx\"\ne2e.args = [\"e2e\", \"mcp\"]\n")]
    [InlineData("# My settings\nmcp_servers = { e2e = { command = \"npx\", env = { APP_ENV = \"staging\" } }, other = { command = \"other\" } }\n")]
    [InlineData("# My settings\nmcp_servers.e2e.env.APP_ENV = \"staging\"\n")]
    [InlineData("# My settings\n[mcp_servers.e2e.env]\nAPP_ENV = \"staging\"\n")]
    [InlineData("# My settings\nmcp_servers = { e2e = { env = { APP_ENV = \"staging\" }, }, }\n")]
    [InlineData("# My settings\n[[profiles]]\nname = \"a\"\n")]
    [InlineData("# My settings\r\n[mcp_servers.e2e]\r\ncommand = \"npx\" # launch\r\n")]
    public void Preserves_Codex_settings_and_comments_when_repairing_or_adding_a_server(string original)
    {
        Write(".codex/config.toml", original);
        var plan = Config.PlanMcpRegistration(_dir, CodexOnly).Registrations[0];
        var before = CodexConfig.ReadCodexConfig(original);
        var after = CodexConfig.ReadCodexConfig(plan.Content);
        var expected = (JsonObject)before.DeepClone();
        var servers = expected["mcp_servers"] as JsonObject ?? new JsonObject();
        var server = servers["e2e"] as JsonObject ?? new JsonObject();
        server["command"] = "e2e";
        server["args"] = new JsonArray("mcp");
        servers["e2e"] = server;
        expected["mcp_servers"] = servers;
        Assert.True(JsonNode.DeepEquals(expected, after), after.ToJsonString());
        foreach (var setting in new[] { "APP_ENV = \"staging\"", "command = \"other\"", "enabled = false", "# My settings", "# server", "# launch" })
        {
            if (original.Contains(setting, StringComparison.Ordinal))
            {
                Assert.Contains(setting, plan.Content, StringComparison.Ordinal);
            }
        }

        Assert.True(plan.Existing);
        Write(".codex/config.toml", plan.Content);
        Assert.Empty(Config.PlanMcpRegistration(_dir, CodexOnly).Registrations);
    }

    [Fact]
    public void Leaves_a_current_Codex_registration_byte_for_byte_unchanged()
    {
        const string original = "# Keep this comment\n[mcp_servers.e2e]\nargs = ['mcp']\ncommand = 'e2e'\nstartup_timeout_sec = 30\n";
        Write(".codex/config.toml", original);
        Assert.Empty(Config.PlanMcpRegistration(_dir, CodexOnly).Registrations);
        Assert.Equal(original, File.ReadAllText(P(".codex/config.toml")));
    }

    [Fact]
    public void Refuses_invalid_Codex_TOML_without_changing_it()
    {
        const string original = "[mcp_servers.e2e\ncommand = \"node\"\n";
        Write(".codex/config.toml", original);
        var error = Assert.Throws<InvalidOperationException>(() => Config.PlanMcpRegistration(_dir, CodexOnly));
        Assert.Matches("is not valid TOML", error.Message);
        Assert.Empty(Config.FindRegisteredMcpFiles(_dir));
        Assert.Equal(original, File.ReadAllText(P(".codex/config.toml")));
    }

    [Theory]
    [InlineData("mcp_servers = []\n")]
    [InlineData("mcp_servers.e2e = []\n")]
    [InlineData("[mcp_servers.e2e.command]\ninvalid = true\n")]
    public void Refuses_an_invalid_Codex_server_structure_without_writing_broken_TOML(string original)
    {
        Write(".codex/config.toml", original);
        Assert.ThrowsAny<Exception>(() => Config.PlanMcpRegistration(_dir, CodexOnly));
        Assert.Equal(original, File.ReadAllText(P(".codex/config.toml")));
    }

    [Fact]
    public void Merges_into_an_existing_file_keeping_other_servers_keys_and_indentation()
    {
        const string existing = "{\n    \"mcpServers\": {\n        \"figma\": { \"command\": \"figma-mcp\" }\n    },\n    \"other\": true\n}\n";
        Write(".mcp.json", existing);
        var plan = Config.PlanMcpRegistration(_dir, [".mcp.json"]).Registrations[0];
        Assert.True(plan.Existing);
        var expected = JsonNode.Parse("""{ "mcpServers": { "figma": { "command": "figma-mcp" }, "e2e": { "command": "e2e", "args": ["mcp"] } }, "other": true }""");
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(plan.Content)));
        Assert.Contains("\n    \"mcpServers\"", plan.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Plans_nothing_for_a_file_that_already_registers_the_same_entry_and_finds_it()
    {
        Write(".cursor/mcp.json", """{"mcpServers":{"e2e":{"command":"e2e","args":["mcp"]}}}""");
        Assert.Empty(Config.PlanMcpRegistration(_dir, [".cursor/mcp.json"]).Registrations);
        Assert.Equal([".cursor/mcp.json"], Config.FindRegisteredMcpFiles(_dir));
        // A stale entry is repaired, not treated as registered.
        Write(".mcp.json", """{"mcpServers":{"e2e":{"command":"npx","args":["e2e","mcp"]}}}""");
        Assert.Single(Config.PlanMcpRegistration(_dir, [".mcp.json"]).Registrations);
        Assert.Equal([".mcp.json", ".cursor/mcp.json"], Config.FindRegisteredMcpFiles(_dir));
    }

    [Fact]
    public void Keeps_extra_settings_on_an_already_current_server_without_rewriting_the_file()
    {
        const string original = "{\n    \"mcpServers\": {\n        \"e2e\": {\n            \"command\": \"e2e\",\n            \"args\": [\n                \"mcp\"\n            ],\n            \"env\": {\n                \"APP_ENV\": \"staging\"\n            },\n            \"timeout\": 30000\n        }\n    }\n}\n";
        Write(".mcp.json", original);
        Assert.Empty(Config.PlanMcpRegistration(_dir, [".mcp.json"]).Registrations);
        Assert.Equal(original, File.ReadAllText(P(".mcp.json")));
    }

    [Fact]
    public void Repairs_a_stale_launch_command_without_dropping_environment_or_client_settings()
    {
        Write(".mcp.json", """{"mcpServers":{"e2e":{"command":"npx","args":["e2e","mcp"],"env":{"APP_ENV":"staging"},"timeout":30000}},"other":true}""");
        var plan = Config.PlanMcpRegistration(_dir, [".mcp.json"]).Registrations[0];
        var expected = JsonNode.Parse("""{ "mcpServers": { "e2e": { "command": "e2e", "args": ["mcp"], "env": { "APP_ENV": "staging" }, "timeout": 30000 } }, "other": true }""");
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(plan.Content)));
    }

    [Fact]
    public void Refuses_to_overwrite_a_file_that_is_not_JSON()
    {
        Write(".mcp.json", "{ not json");
        var error = Assert.Throws<InvalidOperationException>(() => Config.PlanMcpRegistration(_dir, [".mcp.json"]));
        Assert.Matches("is not valid JSON", error.Message);
        Assert.Empty(Config.FindRegisteredMcpFiles(_dir));
        Assert.Equal("{ not json", File.ReadAllText(P(".mcp.json")));
    }

    [Theory]
    [InlineData(".cursor")]
    [InlineData(".codex")]
    public void Never_plans_a_write_through_a_linked_directory_and_reports_the_link(string directory)
    {
        if (!Symlinks)
        {
            return;
        }

        var elsewhere = Directory.CreateTempSubdirectory("e2e-mcp-elsewhere-").FullName;
        try
        {
            // The target already registers e2e: discovery must not follow the link and call it registered.
            var file = directory == ".cursor" ? "mcp.json" : "config.toml";
            File.WriteAllText(Path.Combine(elsewhere, file), file.EndsWith(".json", StringComparison.Ordinal) ? """{"mcpServers":{"e2e":{"command":"e2e","args":["mcp"]}}}""" : "[mcp_servers.e2e]\ncommand = \"node\"\n");
            Directory.CreateSymbolicLink(P(directory), elsewhere);
            var plan = Config.PlanMcpRegistration(_dir, [directory + "/" + file]);
            Assert.Empty(plan.Registrations);
            Assert.Equal([new McpLink(directory, Paths.OnDiskPath(elsewhere))], plan.Links);
            // Nothing was written through the link, and it is not counted as registered.
            Assert.Equal([file], Directory.EnumerateFileSystemEntries(elsewhere).Select(Path.GetFileName));
            Assert.Empty(Config.FindRegisteredMcpFiles(_dir));
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    public void Never_plans_a_write_through_a_linked_file_and_reports_the_link()
    {
        if (!Symlinks)
        {
            return;
        }

        var elsewhere = Directory.CreateTempSubdirectory("e2e-mcp-elsewhere-").FullName;
        try
        {
            var target = Path.Combine(elsewhere, "real.json");
            // The target already registers e2e: discovery must not follow the link and call it registered.
            const string original = """{"mcpServers":{"e2e":{"command":"e2e","args":["mcp"]},"other":{"command":"x"}}}""";
            File.WriteAllText(target, original);
            File.CreateSymbolicLink(P(".mcp.json"), target);
            var plan = Config.PlanMcpRegistration(_dir, [".mcp.json"]);
            Assert.Empty(plan.Registrations);
            Assert.Equal([new McpLink(".mcp.json", Paths.OnDiskPath(target))], plan.Links);
            // The target is untouched: nothing was read or written through the link, and it is not counted as registered.
            Assert.Equal(original, File.ReadAllText(target));
            Assert.NotNull(new FileInfo(P(".mcp.json")).LinkTarget);
            Assert.Empty(Config.FindRegisteredMcpFiles(_dir));
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }
}
