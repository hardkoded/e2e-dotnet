// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Cli.Tests.CliInit;

/// <summary>
/// Upstream's nested <c>describe('a symlinked skill directory')</c> of <c>cli-init.test.ts</c>. Not ported, since the port
/// has no prompt: "asks before replacing the link with a copy, declining by default" and "is replaced with the link when it
/// dangles beside a chosen .agents/skills and the user agrees". Every test returns early on a host that cannot create
/// symlinks, as upstream's <c>describe.skipIf(!symlinks)</c> skips them.
/// </summary>
public sealed partial class ASymlinkedSkillDirectoryTests : CliInitTestBase
{
    [GeneratedRegex(@"\A---\r?\nname: e2e-dotnet\r?\n")]
    private static partial Regex SkillFrontmatter();

    /// <summary>Links <c>&lt;location&gt;/e2e-dotnet</c> to <paramref name="target"/>, which holds the user's own SKILL.md.</summary>
    private void LinkSkill(string location, string target)
    {
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "SKILL.md"), "mine\n");
        Directory.CreateDirectory(Path_(location));
        Link(Path_(location + "/e2e-dotnet"), target, directory: true);
    }

    /// <summary>The target holds exactly what the user put there.</summary>
    private static void Untouched(string target)
    {
        Assert.Equal(["SKILL.md"], Directory.EnumerateFileSystemEntries(target).Select(Path.GetFileName));
        Assert.Equal("mine\n", File.ReadAllText(Path.Combine(target, "SKILL.md")));
    }

    [Fact]
    public async Task Is_left_alone_under_yes_when_it_points_outside_the_project_and_the_warning_names_it()
    {
        if (!Symlinks)
        {
            return;
        }

        var elsewhere = Directory.CreateTempSubdirectory("e2e-init-elsewhere-").FullName;
        try
        {
            var target = Path.Combine(elsewhere, "e2e-dotnet");
            LinkSkill(".claude/skills", target);
            Assert.Equal(0, (await RunInitAsync()).ExitCode);
            Untouched(target);
            Assert.True(IsLink(Path_(".claude/skills/e2e-dotnet")));
            Assert.Contains("Symlink, not touching: .claude/skills/e2e-dotnet -> " + Real(target), Output, StringComparison.Ordinal);
            Assert.DoesNotContain(".claude/skills/e2e-dotnet/ (", Output, StringComparison.Ordinal);
            Assert.NotNull(Json("e2e.config.json")["targets"]);
            Assert.NotNull(Json(".mcp.json")["mcpServers"]!["e2e"]);
            Assert.Contains(".e2e/cache/", Read(".gitignore"), StringComparison.Ordinal);
            // The link counts as the installed location, so no other one is added.
            Assert.False(Exists(".agents"));
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    public async Task Is_left_alone_under_yes_without_reading_what_it_leads_to_even_a_directory_named_SKILL_md()
    {
        if (!Symlinks)
        {
            return;
        }

        var elsewhere = Directory.CreateTempSubdirectory("e2e-init-elsewhere-").FullName;
        try
        {
            var target = Path.Combine(elsewhere, "e2e-dotnet");
            Directory.CreateDirectory(Path.Combine(target, "SKILL.md"));
            Directory.CreateDirectory(Path_(".claude/skills"));
            Link(Path_(".claude/skills/e2e-dotnet"), target, directory: true);
            Assert.Equal(0, (await RunInitAsync()).ExitCode);
            Assert.Contains("Symlink, not touching: .claude/skills/e2e-dotnet -> " + Real(target), Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Broken, not touching", Output, StringComparison.Ordinal);
            Assert.True(Directory.Exists(Path.Combine(target, "SKILL.md")));
            Assert.Equal(["SKILL.md"], Directory.EnumerateFileSystemEntries(target).Select(Path.GetFileName));
            Assert.NotNull(Json("e2e.config.json")["targets"]);
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    public async Task Is_left_alone_under_yes_when_it_points_inside_the_project()
    {
        if (!Symlinks)
        {
            return;
        }

        var target = Path_("shared/e2e-dotnet");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "SKILL.md"), "mine\n");
        Directory.CreateDirectory(Path_(".agents/skills"));
        Link(Path_(".agents/skills/e2e-dotnet"), Path.Combine("..", "..", "shared", "e2e-dotnet"), directory: true);
        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        Untouched(target);
        Assert.Contains("Symlink, not touching: .agents/skills/e2e-dotnet -> " + Real(target), Output, StringComparison.Ordinal);
        Assert.False(Exists(".claude"));
    }

    [Fact]
    public async Task Is_left_alone_under_yes_when_it_dangles_and_the_warning_names_the_target_as_written()
    {
        if (!Symlinks)
        {
            return;
        }

        Directory.CreateDirectory(Path_(".claude/skills"));
        var link = Path_(".claude/skills/e2e-dotnet");
        Link(link, Path.Combine("..", "..", "gone", "e2e-dotnet"), directory: true);
        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        Assert.Contains("Symlink, not touching: .claude/skills/e2e-dotnet -> " + Path.Combine(Dir, "gone", "e2e-dotnet"), Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Broken, not touching", Output, StringComparison.Ordinal);
        Assert.True(IsLink(link));
        Assert.False(Exists("gone"));
        // A link to nothing is no installed copy, so --yes takes every location and the other one gets the skill.
        Assert.Matches(SkillFrontmatter(), Read(".agents/skills/e2e-dotnet/SKILL.md"));
    }

    [Fact]
    public async Task Is_left_alone_under_yes_beside_a_copy_in_agents_skills_and_the_warning_names_it()
    {
        if (!Symlinks)
        {
            return;
        }

        var elsewhere = Directory.CreateTempSubdirectory("e2e-init-elsewhere-").FullName;
        try
        {
            var target = Path.Combine(elsewhere, "e2e-dotnet");
            LinkSkill(".claude/skills", target);
            WriteCopy(".agents/skills");
            Assert.Equal(0, (await RunInitAsync()).ExitCode);
            Assert.Contains("Symlink, not touching: .claude/skills/e2e-dotnet -> " + Real(target), Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Linked", Output, StringComparison.Ordinal);
            Assert.True(IsLink(Path_(".claude/skills/e2e-dotnet")));
            Untouched(target);
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    public async Task Gets_a_copy_in_claude_skills_when_the_agents_skills_copy_is_left_alone()
    {
        if (!Symlinks)
        {
            return;
        }

        var elsewhere = Directory.CreateTempSubdirectory("e2e-init-elsewhere-").FullName;
        try
        {
            var target = Path.Combine(elsewhere, "e2e-dotnet");
            LinkSkill(".agents/skills", target);
            WriteCopy(".claude/skills", "stale\n");
            Assert.Equal(0, (await RunInitAsync()).ExitCode);
            Assert.Contains("Symlink, not touching: .agents/skills/e2e-dotnet -> " + Real(target), Output, StringComparison.Ordinal);
            Assert.True(Directory.Exists(Path_(".claude/skills/e2e-dotnet")));
            Assert.False(IsLink(Path_(".claude/skills/e2e-dotnet")));
            Assert.Matches(SkillFrontmatter(), Read(".claude/skills/e2e-dotnet/SKILL.md"));
            Assert.Contains("Updated .claude/skills/e2e-dotnet/", Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Linked", Output, StringComparison.Ordinal);
            Untouched(target);
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    public async Task Is_skipped_without_a_question_when_a_parent_is_the_link()
    {
        if (!Symlinks)
        {
            return;
        }

        var shared = Path_("shared");
        var target = Path.Combine(shared, "e2e-dotnet");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "SKILL.md"), "mine\n");
        Directory.CreateDirectory(Path_(".claude"));
        Link(Path_(".claude/skills"), shared, directory: true);
        Assert.Equal(0, (await RunInitAsync()).ExitCode);
        Assert.Contains("Symlink, not touching: .claude/skills/e2e-dotnet/ (.claude/skills -> " + Real(shared) + ")", Output, StringComparison.Ordinal);
        Untouched(target);
        Assert.True(IsLink(Path_(".claude/skills")));
    }
}
