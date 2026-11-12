// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.AgentScrollPages.ScrollPagesKit;

namespace E2E.Tests.AgentScrollPages;

/// <summary>Upstream's <c>describe('with a clock ticking on the screen')</c>, nested in <c>describe('scroll_to a text, reads per page')</c> (<c>agent-scroll-pages.test.ts</c>).</summary>
public sealed class WithAClockTickingOnTheScreenTests
{
    [Fact]
    public async Task Reads_each_page_once_when_the_swipe_landed()
    {
        var result = await ScrollToAsync("Row 13", Turning.Now, withClock: true);
        Assert.Null(result.Error);
        Assert.Contains("Row 13", result.Found, StringComparison.Ordinal);
        Assert.Equal(4, result.Swipes);
        Assert.Equal([1, 1, 1, 1], result.CapturesPerPage);
    }

    [Fact]
    public async Task Does_not_take_the_tick_for_a_turned_page_and_does_not_page_past_the_row()
    {
        var result = await ScrollToAsync("Row 4", Turning.Late, withClock: true);
        Assert.Null(result.Error);
        Assert.Contains("Row 4", result.Found, StringComparison.Ordinal);
        Assert.Equal(1, result.Swipes);
        Assert.All(result.CapturesPerPage, captures => Assert.True(captures >= 2));
    }

    [Fact]
    public async Task Still_ends_a_list_that_stops_moving_with_LOCATOR_NOT_FOUND_not_at_the_screen_cap()
    {
        var result = await ScrollToAsync("Row 99", Turning.Never, withClock: true);
        Assert.Equal("LOCATOR_NOT_FOUND", result.Error);
        Assert.Equal(3, result.Swipes);
    }
}
