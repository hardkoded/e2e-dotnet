// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;
using ToolOutputs = global::E2E.ToolOutput;

namespace E2E.Tests.ToolOutput;

/// <summary>Upstream's <c>describe('boundToolOutput')</c> (<c>tool-output.test.ts</c>).</summary>
public sealed partial class BoundToolOutputTests
{
    private const int MaxLines = ToolOutputs.MaxToolOutputLines;
    private const int MaxBytes = ToolOutputs.MaxToolOutputBytes;

    [GeneratedRegex(@"^\[tool output truncated: showing 399 of 500 lines, .* the lines limit was reached\.")]
    private static partial Regex LinesNotice();

    // The whole returned text, notice included, must fit the advertised limits.
    private static void ExpectWithinLimits(string text, int maxLines = MaxLines, int maxBytes = MaxBytes)
    {
        Assert.True(text.Split('\n').Length <= maxLines);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= maxBytes);
    }

    [Fact]
    public void Passes_a_result_within_both_limits_through_untouched()
    {
        var text = string.Join('\n', Enumerable.Range(0, 10).Select(i => $"line {i}"));
        Assert.Equal(new BoundToolOutput(text, false, null, 10, text.Length), ToolOutputs.BoundToolOutput(text));
    }

    [Fact]
    public void Cuts_on_the_line_limit_without_splitting_a_line_and_says_what_is_missing()
    {
        var text = string.Join('\n', Enumerable.Range(0, MaxLines + 100).Select(i => $"line {i}"));
        var bound = ToolOutputs.BoundToolOutput(text);
        Assert.True(bound.Truncated);
        Assert.Equal("lines", bound.By);
        ExpectWithinLimits(bound.Text);
        var lines = bound.Text.Split('\n');
        Assert.Equal(MaxLines, lines.Length);
        Assert.Equal($"line {MaxLines - 2}", lines[MaxLines - 2]);
        Assert.Matches(LinesNotice(), lines[^1]);
    }

    [Fact]
    public void Cuts_on_the_byte_limit_first_when_the_lines_are_long()
    {
        var text = string.Join('\n', Enumerable.Repeat(new string('y', 1000), 50));
        var bound = ToolOutputs.BoundToolOutput(text);
        Assert.Equal("bytes", bound.By);
        ExpectWithinLimits(bound.Text);
        var lines = bound.Text.Split('\n');
        Assert.Equal(17, lines.Length);
        Assert.All(lines.Take(16), line => Assert.Equal(new string('y', 1000), line));
        Assert.Contains("showing 16 of 50 lines", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_head_of_a_single_line_that_is_longer_than_the_whole_budget()
    {
        var bound = ToolOutputs.BoundToolOutput(new string('z', 40_000), new ToolOutputLimits(10, 1024));
        Assert.True(bound.Truncated);
        ExpectWithinLimits(bound.Text, 10, 1024);
        Assert.StartsWith(new string('z', 768), bound.Text, StringComparison.Ordinal);
        Assert.Contains("showing 1 of 1 lines, 768 B of 39.1 KB", bound.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Counts_bytes_not_characters_for_multi_byte_text_and_never_cuts_inside_a_character()
    {
        var bound = ToolOutputs.BoundToolOutput(new string('ż', 600), new ToolOutputLimits(10, 1024));
        Assert.True(bound.Truncated);
        Assert.Equal(1200, bound.TotalBytes);
        ExpectWithinLimits(bound.Text, 10, 1024);
        Assert.DoesNotContain('�',bound.Text);
    }
}
