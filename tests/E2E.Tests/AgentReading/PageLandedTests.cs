// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.AgentReading.ReadingKit;

namespace E2E.Tests.AgentReading;

/// <summary>
/// Upstream's <c>describe('pageLanded')</c> (<c>agent-reading.test.ts</c>). Its nested <c>describe('with a status line beside
/// the list')</c> is <see cref="PageLandedWithAStatusLineBesideTheListTests"/>.
/// </summary>
public sealed class PageLandedTests
{
    private static PageReading Rows(params string[] texts) =>
        Reading.ReadPage(IndexOf([.. texts.Select((text, at) => Node("r" + at, role: "listitem", text: text, rect: new BoundingBox(0, at * 40, 300, 40)))]));

    [Fact]
    public void Accepts_a_page_that_reads_differently_and_is_no_sparser()
    {
        Assert.True(Reading.PageLanded(Rows("Row 1", "Row 2", "Row 3"), Rows("Row 4", "Row 5", "Row 6")));
        Assert.True(Reading.PageLanded(Rows("Row 1", "Row 2"), Rows("Row 3", "Row 4", "Row 5")));
    }

    [Fact]
    public void Declines_a_page_that_reads_the_same_as_the_scroll_not_rendered_yet_or_the_end_of_the_list()
    {
        Assert.False(Reading.PageLanded(Rows("Row 1", "Row 2"), Rows("Row 1", "Row 2")));
    }

    [Fact]
    public void Declines_a_thinner_page_a_windowed_list_between_the_scroll_and_the_rows_it_draws()
    {
        Assert.False(Reading.PageLanded(Rows("Row 1", "Row 2", "Row 3"), Rows("Row 4")));
        Assert.False(Reading.PageLanded(Rows("Row 1", "Row 2", "Row 3"), Rows()));
    }

    [Fact]
    public void Wants_a_third_of_the_lines_to_be_new()
    {
        string[] nine = ["Row 1", "Row 2", "Row 3", "Row 4", "Row 5", "Row 6", "Row 7", "Row 8", "Row 9"];
        Assert.True(Reading.PageLanded(Rows(nine), Rows([.. nine[..6], "Row 10", "Row 11", "Row 12"])));
        Assert.False(Reading.PageLanded(Rows(nine), Rows([.. nine[..7], "Row 10", "Row 11"])));
    }
}
