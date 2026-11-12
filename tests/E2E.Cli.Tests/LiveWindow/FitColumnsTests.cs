// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;

namespace E2E.Cli.Tests.LiveWindow;

/// <summary>Upstream: packages/e2e/tests/unit/live-window.test.ts, describe('fitColumns').</summary>
public sealed class FitColumnsTests
{
    private const string Family = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";

    private const string Heart = "❤️";

    [Fact]
    public void Leaves_text_that_fits_alone_and_clips_by_column_with_the_ellipsis_inside_the_budget()
    {
        Assert.Equal("abc", Format.FitColumns("abc", 3));
        Assert.Equal("ab…", Format.FitColumns("abcd", 3));
        Assert.Equal("日本語…", Format.FitColumns("日本語テスト", 8));
        Assert.Equal($"ab{Heart}…", Format.FitColumns($"ab{Heart}cd", 5));
        Assert.Equal($"a{Family}…", Format.FitColumns($"a{Family}bc", 4));
        Assert.Equal(59, Format.VisibleWidth(Format.FitColumns(new string('日', 60), 60)));
        Assert.Equal("éé…", Format.FitColumns(string.Concat(Enumerable.Repeat("é", 5)), 3));
    }
}
