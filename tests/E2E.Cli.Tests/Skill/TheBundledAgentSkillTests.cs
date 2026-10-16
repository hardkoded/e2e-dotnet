// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using BundledSkill = E2E.Cli.Skill;

namespace E2E.Cli.Tests.Skill;

public sealed class TheBundledAgentSkillTests
{
    [Fact]
    public void Starts_with_a_SKILL_md_whose_frontmatter_names_the_skill_after_its_directory()
    {
        var files = BundledSkill.ReadFiles();
        var skill = files[0];
        Assert.Equal("SKILL.md", skill.Relative);
        Assert.Matches(new Regex("^---\r?\nname: e2e-dotnet\r?\ndescription: .+\r?\n---\r?\n"), skill.Content);
        Assert.Equal(BundledSkill.Topics().Select(topic => "references/" + topic + ".md"), files.Skip(1).Select(file => file.Relative));
        var description = Regex.Match(skill.Content, "^description: (.+?)\r?$", RegexOptions.Multiline).Groups[1].Value;
        Assert.InRange(description.Length, 1, 1024);
        Assert.DoesNotContain(": ", description, StringComparison.Ordinal);
    }

    [Fact]
    // The port adds one writing-tests topic per test framework to upstream's eight.
    public void Offers_one_topic_per_reference_file_and_lists_every_topic_in_SKILL_md()
    {
        var topics = BundledSkill.Topics();
        Assert.Equal(["agent", "bug-bash", "debugging", "explore", "mcp", "mobile", "running", "setup", "writing-tests", "writing-tests-nunit", "writing-tests-xunit"], topics);
        var overview = BundledSkill.ReadGuide(null) ?? "";
        var linked = Regex.Matches(overview, @"\[references/([a-z-]+)\.md\]\(references/\1\.md\)").Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal);
        Assert.Equal(topics, linked);
        foreach (var topic in topics)
        {
            Assert.Contains("| `" + topic + "` |", overview, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Opens_every_reference_longer_than_100_lines_with_a_Contents_list_of_its_sections()
    {
        // Agents sometimes read only the head of a long file; the list shows them what the rest holds.
        foreach (var file in BundledSkill.ReadFiles().Where(skillFile => skillFile.Relative.StartsWith("references/", StringComparison.Ordinal)))
        {
            if (file.Content.Split('\n').Length <= 100)
            {
                continue;
            }

            var sections = SectionHeadings(file.Content);
            Assert.True(sections.Count > 0 && sections[0] == "Contents", file.Relative);
            var listed = Regex.Match(file.Content, @"\n## Contents\r?\n\r?\n((?:- .*\r?\n)+)").Groups[1].Value;
            Assert.True(
                listed.TrimEnd().Split('\n').Select(line => line.TrimEnd('\r')["- ".Length..]).SequenceEqual(sections.Skip(1)),
                file.Relative);
        }
    }

    [Fact]
    // Upstream's overview names `npx e2e guide <topic>`; the .NET tool is `e2e` itself.
    public void Prints_the_overview_without_its_frontmatter_and_each_topic_verbatim()
    {
        var overview = BundledSkill.ReadGuide(null) ?? "";
        Assert.StartsWith("# e2e", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("\n---\n", overview, StringComparison.Ordinal);
        Assert.Contains("e2e guide <topic>", overview, StringComparison.Ordinal);
        foreach (var topic in BundledSkill.Topics())
        {
            var text = BundledSkill.ReadGuide(topic) ?? "";
            Assert.StartsWith("# ", text, StringComparison.Ordinal);
            Assert.Equal(BundledSkill.ReadFiles().Single(file => file.Relative == "references/" + topic + ".md").Content, text);
        }

        Assert.Null(BundledSkill.ReadGuide("nope"));
        Assert.Null(BundledSkill.ReadGuide("../SKILL"));
    }

    [Fact]
    public void Runs_the_installed_CLI_as_npx_e2e_without_no_install_or_the_scoped_package_name()
    {
        foreach (var file in BundledSkill.ReadFiles())
        {
            Assert.False(file.Content.Contains("--no-install", StringComparison.Ordinal), file.Relative);
            Assert.False(Regex.IsMatch(file.Content, @"npx @e2edev/e2e (run|guide)\b"), file.Relative);
        }
    }

    /// <summary>The <c>## </c> headings of a markdown file, in order, skipping fenced code blocks.</summary>
    private static List<string> SectionHeadings(string markdown)
    {
        var headings = new List<string>();
        var fenced = false;
        foreach (var line in markdown.Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
            }

            if (!fenced && line.StartsWith("## ", StringComparison.Ordinal))
            {
                headings.Add(line["## ".Length..].Trim());
            }
        }

        return headings;
    }
}
