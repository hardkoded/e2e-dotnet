// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;

namespace E2E.Cli.Tests.LiveWindow;

/// <summary>Upstream: packages/e2e/tests/unit/live-window.test.ts, describe('visibleWidth'). All 3 <c>it</c> are ported.</summary>
public sealed class VisibleWidthTests
{
    private const string Family = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";

    private const string Heart = "❤️";

    private const string KeycapOne = "1️⃣";

    private const string Flag = "\U0001F1F5\U0001F1F1";

    [Fact]
    public void Counts_terminal_columns_per_grapheme_cluster_not_code_units_or_code_points()
    {
        Assert.Equal(3, Format.VisibleWidth("abc"));
        Assert.Equal(4, Format.VisibleWidth("日本"));
        Assert.Equal(2, Format.VisibleWidth("\U0001F600"));
        Assert.Equal(2, Format.VisibleWidth(Heart));
        Assert.Equal(2, Format.VisibleWidth(Family));
        Assert.Equal(2, Format.VisibleWidth(KeycapOne));
        Assert.Equal(2, Format.VisibleWidth(Flag));
        Assert.Equal(1, Format.VisibleWidth("é"));
        Assert.Equal(2, Format.VisibleWidth("\U0001F44D\U0001F3FD"));
        Assert.Equal(1, Format.VisibleWidth("☁︎"));
        Assert.Equal(2, Format.VisibleWidth("\u001b[31mab\u001b[0m"));
        Assert.Equal(13, Format.VisibleWidth("❯ ✓ × └ ↳ … ✳"));
    }

    [Fact]
    public void Paints_a_combining_mark_in_its_bases_column_a_Hangul_medial_or_final_jamo_in_the_leads_and_a_fullwidth_form_in_two()
    {
        Assert.Equal(1, Format.VisibleWidth("é"));
        Assert.Equal(3, Format.VisibleWidth(string.Concat(Enumerable.Repeat("é", 3))));

        // 한 spelled as lead, medial, and final jamo: one syllable block, two columns.
        Assert.Equal(2, Format.VisibleWidth("한"));
        Assert.Equal(0, Format.VisibleWidth("ᅡᆫ"));
        Assert.Equal(6, Format.VisibleWidth("ＡＢＣ"));
        Assert.Equal(6, Format.VisibleWidth("１２３"));
        Assert.Equal(2, Format.VisibleWidth("￥"));
    }

    [Fact]
    public void Gives_a_format_character_other_than_the_joiner_and_the_variation_selectors_no_column()
    {
        // Zero width space, left-to-right mark, word joiner, byte order mark.
        Assert.Equal(2, Format.VisibleWidth("a​b"));
        Assert.Equal(0, Format.VisibleWidth("‎⁠﻿"));
        Assert.Equal(2, Format.VisibleWidth("ab‎"));
    }
}
