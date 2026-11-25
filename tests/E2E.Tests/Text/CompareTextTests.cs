// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E.Tests.Text;

public sealed class CompareTextTests
{
    [Fact]
    public void Contains_mode_uses_a_case_sensitive_substring_for_an_exact_string()
    {
        Assert.True(
            TextRules.Compare(
                "Hello World",
                TextMatch.FromString("World"),
                contains: true,
                normalize: true,
                ignoreCase: null
            )
        );
        Assert.False(
            TextRules.Compare(
                "Hello World",
                TextMatch.FromString("world"),
                contains: true,
                normalize: true,
                ignoreCase: null
            )
        );
    }

    [Fact]
    public void Folds_case_for_an_exact_string_under_ignoreCase_and_always_for_a_non_exact_one()
    {
        Assert.True(
            TextRules.Compare("  HELLO   world ", TextMatch.FromString("hello world"), false, true, ignoreCase: true)
        );
        Assert.False(
            TextRules.Compare("Hello World!", TextMatch.FromString("hello world"), false, true, ignoreCase: true)
        );
        Assert.True(TextRules.Compare("Hello World!", TextMatch.FromString("WORLD"), true, true, ignoreCase: true));
        Assert.True(TextRules.MatchesText("Hello World!", TextMatch.FromString("WORLD"), exact: false));
    }

    [Fact]
    public void Normalize_false_compares_the_raw_strings_newlines_and_trailing_spaces_included()
    {
        const string value = "line1\n\nline2  ";
        Assert.False(
            TextRules.Compare(
                value,
                TextMatch.FromString("line1 line2"),
                contains: false,
                normalize: false,
                ignoreCase: null
            )
        );
        Assert.True(
            TextRules.Compare(value, TextMatch.FromString(value), contains: false, normalize: false, ignoreCase: null)
        );
        Assert.True(
            TextRules.Compare(
                value,
                TextMatch.FromRegex(new Regex("^line1\\n\\nline2 {2}$")),
                contains: false,
                normalize: false,
                ignoreCase: null
            )
        );
        Assert.True(
            TextRules.Compare(
                value,
                TextMatch.FromString("line1 line2"),
                contains: false,
                normalize: true,
                ignoreCase: null
            )
        );
    }
}
