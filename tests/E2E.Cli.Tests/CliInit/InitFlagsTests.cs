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
    [InlineData("-x")]
    [InlineData("--endpoint", "https://user:key@llm.example/v1")]
    public void Rejects_a_flag_without_a_usable_value_or_an_unknown_option(params string[] args)
    {
        var (options, error) = InitFlags.Parse(args);
        Assert.Null(options);
        Assert.StartsWith("error:", error, StringComparison.Ordinal);
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
