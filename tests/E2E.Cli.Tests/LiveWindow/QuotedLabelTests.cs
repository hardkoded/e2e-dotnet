// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;

namespace E2E.Cli.Tests.LiveWindow;

/// <summary>Upstream: packages/e2e/tests/unit/live-window.test.ts, describe('quotedLabel').</summary>
public sealed class QuotedLabelTests
{
    [Fact]
    public void Spends_its_72_column_budget_in_columns_so_a_CJK_label_keeps_half_the_glyphs_an_ASCII_one_does()
    {
        Assert.Equal($"\"{new string('a', 72)}\"", ListSteps.QuotedLabel(new string('a', 72)));
        Assert.Equal($"\"{new string('日', 36)}\"", ListSteps.QuotedLabel(new string('日', 36)));
        var clipped = ListSteps.QuotedLabel(new string('日', 40));
        Assert.Equal($"\"{new string('日', 35)}…\"", clipped);
        Assert.Equal(73, Format.VisibleWidth(clipped));
    }

    [Fact]
    public void Clips_to_a_row_budget_by_column_never_under_24_columns()
    {
        Assert.Equal($"\"{new string('日', 19)}…\"", ListSteps.QuotedLabel(new string('日', 30), 40));
        Assert.Equal($"\"{new string('日', 11)}…\"", ListSteps.QuotedLabel(new string('日', 30), 10));
        Assert.Equal(25, Format.VisibleWidth(ListSteps.QuotedLabel(new string('日', 30), 10)));
    }
}
