// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.AgentReading.ReadingKit;

namespace E2E.Tests.AgentReading;

/// <summary>Upstream's <c>describe('a clock on the screen')</c> (<c>agent-reading.test.ts</c>).</summary>
public sealed class AClockOnTheScreenTests
{
    private static SemanticNode Clock(string time) => Node("clock", role: "status", name: "Clock", text: time, rect: new BoundingBox(0, 0, 100, 20));

    private static SemanticNode Row(string text) => Node("r", role: "listitem", text: text, rect: new BoundingBox(0, 40, 300, 40));

    [Theory]
    [InlineData("12:06")]
    [InlineData("9:59:59")]
    public void Is_not_a_page_that_moved_however_it_ticked(string time)
    {
        var before = Reading.ReadPage(IndexOf(Clock("12:05"), Row("Row 1")));
        var after = Reading.ReadPage(IndexOf(Clock(time), Row("Row 1")));
        Assert.False(Reading.PageMoved(before, after));
        Assert.False(Reading.PageLanded(before, after));
    }

    [Fact]
    public void Still_lets_a_page_that_moved_read_differently()
    {
        static SemanticNode[] Rows(params string[] texts) =>
            [.. texts.Select((text, at) => Node("r" + at, role: "listitem", text: text, rect: new BoundingBox(0, 40 + at * 40, 300, 40)))];

        var before = Reading.ReadPage(IndexOf([Clock("12:05"), .. Rows("Row 1", "Row 2")]));
        var after = Reading.ReadPage(IndexOf([Clock("12:06"), .. Rows("Row 3", "Row 4")]));
        Assert.True(Reading.PageMoved(before, after));
        Assert.True(Reading.PageLanded(before, after));
    }
}
