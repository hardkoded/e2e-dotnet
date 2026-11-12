// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.CliInit;

/// <summary>.NET-only: upstream parses the flags with commander and tests none of this. <c>InitFlags.Parse</c> reads them by hand.</summary>
public sealed class InitFlagsTests : CliInitTestBase
{
    [Theory]
    [InlineData("--engine", "--yes")]
    [InlineData("--skills=")]
    [InlineData("--mcp", " , ")]
    [InlineData("--engine", "web,mobile")]
    [InlineData("-x")]
    [InlineData("--endpoint", "https://user:key@llm.example/v1")]
    public void Rejects_a_flag_without_a_usable_value_or_an_unknown_option(params string[] args)
    {
        var (options, error) = InitFlags.Parse(args);
        Assert.Null(options);
        Assert.StartsWith("error:", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--example", "expo")]
    [InlineData("--framework", "mstest")]
    [InlineData("--example", "TodoMvc", "--engine", "web")]
    [InlineData("--example", "TodoMvc", "--gateway", "none")]
    [InlineData("--example", "TodoMvc", "--framework", "xunit")]
    public void Rejects_an_unknown_example_or_framework_and_an_example_beside_the_flags_it_brings_its_own_files_for(params string[] args)
    {
        var (options, error) = InitFlags.Parse(args);
        Assert.Null(options);
        Assert.StartsWith("error:", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_example_in_any_case_and_the_install_and_framework_flags()
    {
        var (options, error) = InitFlags.Parse(["--example", "todomvc", "--install"]);
        Assert.Null(error);
        Assert.Equal("TodoMvc", options!.Example);
        Assert.True(options.Install);

        (options, error) = InitFlags.Parse(["--no-install", "--framework=xunit"]);
        Assert.Null(error);
        Assert.False(options!.Install);
        Assert.Equal("xunit", options.Framework);
        Assert.Null(InitFlags.Parse([]).Options!.Install);
    }

    [Fact]
    public void Lists_every_example_and_framework_in_the_usage()
    {
        Assert.All(Examples.All, example => Assert.Contains(example.Id, InitFlags.Usage, StringComparison.Ordinal));
        Assert.All(Frameworks.All, framework => Assert.Contains(framework.Id, InitFlags.Usage, StringComparison.Ordinal));
    }

    [Fact]
    public void Merges_a_repeated_list_flag_and_drops_duplicates()
    {
        var (options, error) = InitFlags.Parse(["--skills", ".agents/skills", "--skills=.claude/skills,.agents/skills"]);
        Assert.Null(error);
        Assert.Equal([".agents/skills", ".claude/skills"], options!.Skills);
    }

    [Fact]
    public void Merges_repeated_mcp_flags_like_comma_separated_ones_and_names_the_codex_file_in_the_unknown_value_error()
    {
        var (options, error) = InitFlags.Parse(["--mcp", ".cursor/mcp.json", "--mcp", ".mcp.json,.codex/config.toml"]);
        Assert.Null(error);
        Assert.Equal([".cursor/mcp.json", ".mcp.json", ".codex/config.toml"], options!.Mcp);

        (options, error) = InitFlags.Parse(["--mcp", "claude"]);
        Assert.Null(options);
        Assert.Equal("error: --mcp: unknown MCP config file \"claude\"; expected .mcp.json, .cursor/mcp.json, .codex/config.toml, none", error);
    }

    [Fact]
    public void Reads_values_in_both_forms_and_the_directory()
    {
        var (options, error) = InitFlags.Parse(["apps/web", "--yes", "--engine=web", "--skills", ".claude/skills/"]);
        Assert.Null(error);
        Assert.True(options!.Yes);
        Assert.Equal("apps/web", options.Directory);
        Assert.Equal(["web"], options.Engines);
        Assert.Equal([".claude/skills"], options.Skills);
    }

    [Fact]
    public async Task Repairs_an_MCP_entry_in_place_keeping_the_order_of_servers_and_keys()
    {
        const string original = "{\n  \"clientSetting\": true,\n  \"mcpServers\": {\n    \"a\": { \"command\": \"a\" },\n    \"e2e\": { \"command\": \"npx\", \"args\": [\"e2e\", \"mcp\"], \"env\": { \"K\": \"v\" } },\n    \"z\": { \"command\": \"z\" }\n  },\n  \"last\": 1\n}\n";
        await File.WriteAllTextAsync(Path_(".mcp.json"), original);
        Assert.Equal(0, (await RunInitAsync(new InitOptions { Yes = true, Mcp = [".mcp.json"], Skills = [] })).ExitCode);
        var json = Json(".mcp.json").AsObject();
        Assert.Equal(["clientSetting", "mcpServers", "last"], json.Select(pair => pair.Key));
        var servers = json["mcpServers"]!.AsObject();
        Assert.Equal(["a", "e2e", "z"], servers.Select(pair => pair.Key));
        Assert.Equal("e2e", (string?)servers["e2e"]!["command"]);
        Assert.Equal("v", (string?)servers["e2e"]!["env"]!["K"]);
    }
}
