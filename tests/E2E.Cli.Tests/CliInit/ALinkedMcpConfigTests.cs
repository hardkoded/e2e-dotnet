// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.CliInit;

/// <summary>
/// Upstream's nested <c>describe('a linked MCP config')</c> of <c>cli-init.test.ts</c>. The port creates a directory
/// symlink in place of a Windows junction, so both tests return early on a host that cannot create symlinks.
/// </summary>
public sealed class ALinkedMcpConfigTests : CliInitTestBase
{
    [Fact]
    public async Task Is_left_alone_under_yes_the_warning_names_it_and_the_target_keeps_its_own_servers()
    {
        if (!Symlinks)
        {
            return;
        }

        var elsewhere = Directory.CreateTempSubdirectory("e2e-init-elsewhere-").FullName;
        try
        {
            var target = Path.Combine(elsewhere, "mcp.json");
            const string original = "{\"mcpServers\":{\"figma\":{\"command\":\"figma-mcp\"}}}\n";
            await File.WriteAllTextAsync(target, original);
            Link(Path_(".mcp.json"), target, directory: false);
            Assert.Equal(0, (await RunInitAsync()).ExitCode);
            Assert.Contains("Symlink, not touching: .mcp.json -> " + Real(target) + " (e2e mcp server)", Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Updated .mcp.json", Output, StringComparison.Ordinal);
            // The linked file is untouched and still a link; the other location is written normally.
            Assert.Equal(original, await File.ReadAllTextAsync(target));
            Assert.True(IsLink(Path_(".mcp.json")));
            Assert.NotNull(Json(".cursor/mcp.json")["mcpServers"]!["e2e"]);
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    public async Task Is_left_alone_when_a_parent_directory_is_the_link_and_nothing_is_written_through_it()
    {
        if (!Symlinks)
        {
            return;
        }

        var elsewhere = Directory.CreateTempSubdirectory("e2e-init-elsewhere-").FullName;
        try
        {
            Link(Path_(".cursor"), elsewhere, directory: true);
            Assert.Equal(0, (await RunInitAsync()).ExitCode);
            Assert.Contains("Symlink, not touching: .cursor -> " + Real(elsewhere) + " (e2e mcp server)", Output, StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFileSystemEntries(elsewhere));
            Assert.NotNull(Json(".mcp.json")["mcpServers"]!["e2e"]);
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }
}
