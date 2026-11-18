// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Cli.Tests.InitCi;

/// <summary>Upstream's <c>describe('the pinned actions')</c>: what init writes and what the docs and the skill show are the same pins.</summary>
public sealed partial class ThePinnedActionsTests : InitCiTestBase
{
    [GeneratedRegex(@"uses: ([\w.-]+/[\w.-]+)@(\S+ # \S+)")]
    private static partial Regex Pin();

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "skills", "e2e-dotnet")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("the repository root was not found");
    }

    private static List<(string Action, string Pin)> PinsIn(string text) =>
        [
            .. Pin()
                .Matches(text)
                .Select(m => (m.Groups[1].Value, m.Groups[1].Value + "@" + m.Groups[2].Value))
                .Distinct(),
        ];

    [Fact]
    // Partial: the pins are in the docs site and the skill, and setup-dotnet is the tag the repository's own workflows use.
    public void Partial_Match_the_pins_in_the_docs_and_the_e2e_skill_so_a_user_copying_either_gets_what_init_writes()
    {
        var pins = PinsIn(Ci.RenderGithubWorkflow(Base with { Jobs = AllJobs }));
        Assert.Contains(pins, pin => pin.Action == "actions/checkout");
        var root = RepositoryRoot();
        var pages = Directory
            .EnumerateFiles(
                Path.Combine(root, "docs-site", "src", "content", "docs"),
                "*.md*",
                SearchOption.AllDirectories
            )
            .Concat(
                Directory.EnumerateFiles(
                    Path.Combine(root, "skills", "e2e-dotnet"),
                    "*.md",
                    SearchOption.AllDirectories
                )
            );
        var mismatches = pages
            .SelectMany(file =>
                PinsIn(File.ReadAllText(file))
                    .Where(pin => pins.Any(own => own.Action == pin.Action) && !pins.Contains(pin))
                    .Select(pin => Path.GetFileName(file) + ": " + pin.Pin)
            )
            .ToList();
        Assert.Empty(mismatches);
    }
}
