// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.AgentReading.ReadingKit;

namespace E2E.Tests.AgentReading;

/// <summary>Upstream's <c>describe('with a status line beside the list')</c>, nested in <c>describe('pageLanded')</c> (<c>agent-reading.test.ts</c>).</summary>
public sealed class PageLandedWithAStatusLineBesideTheListTests
{
    private static readonly string[] Eight = ["Row 1", "Row 2", "Row 3", "Row 4", "Row 5", "Row 6", "Row 7", "Row 8"];

    private static PageReading Listed(string status, params string[] texts) =>
        Reading.ReadPage(IndexOf([
            Node("status", role: "status", name: "Inbox", text: status, rect: new BoundingBox(0, 0, 300, 20)),
            .. texts.Select((text, at) => Slot("r" + at, text, at, top: 40)),
        ]));

    [Fact]
    public void Declines_a_stale_list_when_only_the_status_line_changed()
    {
        Assert.False(Reading.PageLanded(Listed("3 unread", Eight), Listed("4 unread", Eight)));
    }

    [Fact]
    public void Declines_a_short_screen_where_the_status_line_is_the_only_new_line()
    {
        Assert.False(Reading.PageLanded(Listed("3 unread", "Row 1"), Listed("4 unread", "Row 1")));
        Assert.False(Reading.PageLanded(Listed("3 unread", "Row 1", "Row 2"), Listed("4 unread", "Row 1", "Row 2")));
    }

    [Fact]
    public void Accepts_a_scrolled_list_with_the_status_line_the_same_or_changed()
    {
        string[] scrolled = ["Row 7", "Row 8", "Row 9", "Row 10", "Row 11", "Row 12", "Row 13", "Row 14"];
        Assert.True(Reading.PageLanded(Listed("3 unread", Eight), Listed("3 unread", scrolled)));
        Assert.True(Reading.PageLanded(Listed("3 unread", Eight), Listed("4 unread", scrolled)));
    }

    [Fact]
    public void Accepts_a_list_scrolled_by_a_row_whose_rows_each_move_to_the_slot_above()
    {
        Assert.True(Reading.PageLanded(Listed("3 unread", Eight), Listed("3 unread", [.. Eight[1..], "Row 9"])));
    }
}
