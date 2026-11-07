// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.AgentReading.ReadingKit;

namespace E2E.Tests.AgentReading;

/// <summary>Upstream's <c>describe('rows that differ by a clock-like value alone')</c> (<c>agent-reading.test.ts</c>).</summary>
public sealed class RowsThatDifferByAClockLikeValueAloneTests
{
    private static PageReading Slots(params string[] times) =>
        Reading.ReadPage(IndexOf([.. times.Select((text, at) => Node("s" + at, role: "listitem", text: text, rect: new BoundingBox(0, at * 40, 300, 40)))]));

    private static readonly SemanticNode Header = Node("h", role: "heading", name: "Pick a time", rect: new BoundingBox(0, 0, 300, 40));

    private static SemanticNode Slot(string id, string text, int at) => Node(id, role: "listitem", text: text, rect: new BoundingBox(0, 40 + at * 40, 300, 40));

    [Fact]
    public void Is_a_page_that_moved_a_time_picker_turning_over()
    {
        Assert.True(Reading.PageMoved(Slots("9:00", "9:30", "10:00"), Slots("10:30", "11:00", "11:30")));
        Assert.True(Reading.PageMoved(
            Reading.ReadPage(IndexOf(Header, Slot("a", "9:00", 0), Slot("b", "9:30", 1))),
            Reading.ReadPage(IndexOf(Header, Slot("a", "10:00", 0), Slot("b", "10:30", 1)))));
    }

    [Fact]
    public void Is_not_one_a_swipe_has_landed_on_since_a_clock_beside_the_stale_screen_reads_the_same()
    {
        Assert.False(Reading.PageLanded(Slots("9:00", "9:30", "10:00"), Slots("10:30", "11:00", "11:30")));
    }

    [Fact]
    public void Is_the_same_page_when_none_changed_and_not_one_when_a_single_value_changed()
    {
        Assert.False(Reading.PageMoved(Slots("9:00", "9:30"), Slots("9:00", "9:30")));
        Assert.False(Reading.PageMoved(Slots("9:00", "9:30"), Slots("9:00", "9:31")));
    }

    [Fact]
    public void Is_a_page_that_moved_when_its_one_line_is_the_value_a_pager_or_carousel_with_nothing_beside_it()
    {
        Assert.True(Reading.PageMoved(Slots("10:30"), Slots("11:00")));
        Assert.False(Reading.PageMoved(Slots("10:30"), Slots("10:30")));
        Assert.False(Reading.PageMoved(Slots(), Slots()));
    }
}
