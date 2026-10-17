// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using BundledSkill = E2E.Cli.Skill;

namespace E2E.Cli.Tests.Skill;

// .NET-only: upstream has no mobile reference. The sample app needs the MAUI workload, so the excerpts are compared as text, not compiled.
public sealed class TheMobileReferenceTests
{
    [Fact]
    public void Quotes_the_mobile_sample_verbatim_in_each_excerpt()
    {
        var reference = BundledSkill.ReadGuide("mobile") ?? throw new InvalidOperationException("no mobile reference");
        var excerpts = Regex.Matches(reference, @"excerpt of `(?<file>\w+\.cs)`[^\n]*\r?\n\r?\n```csharp\r?\n(?<code>.*?)```", RegexOptions.Singleline);
        Assert.Equal(3, excerpts.Count);
        foreach (Match excerpt in excerpts)
        {
            var file = excerpt.Groups["file"].Value;
            var sample = Lines(File.ReadAllText(Path.Combine(RepoRoot(), "samples", "MobileApp.E2ETests", file)));
            foreach (var part in Regex.Split(excerpt.Groups["code"].Value, @"^\s*// \.\.\.\s*$", RegexOptions.Multiline))
            {
                var lines = Lines(part);
                Assert.True(lines.Count == 0 || IndexOf(sample, lines) >= 0, file + ": " + string.Join("\n", lines));
            }
        }
    }

    /// <summary>The non-blank lines of <paramref name="text"/>, trimmed, so indentation does not matter.</summary>
    private static List<string> Lines(string text) => text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();

    /// <summary>The index in <paramref name="haystack"/> where <paramref name="needle"/> appears as consecutive lines, or -1.</summary>
    private static int IndexOf(List<string> haystack, List<string> needle)
    {
        for (var start = 0; start + needle.Count <= haystack.Count; start++)
        {
            if (haystack.Skip(start).Take(needle.Count).SequenceEqual(needle))
            {
                return start;
            }
        }

        return -1;
    }

    private static string RepoRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "E2E.slnx")))
        {
            root = root.Parent ?? throw new InvalidOperationException("no E2E.slnx above " + AppContext.BaseDirectory);
        }

        return root.FullName;
    }
}
