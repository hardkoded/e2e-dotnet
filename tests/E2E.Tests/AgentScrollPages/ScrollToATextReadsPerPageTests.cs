// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.AgentScrollPages.ScrollPagesKit;

namespace E2E.Tests.AgentScrollPages;

/// <summary>
/// Upstream's <c>describe('scroll_to a text, reads per page')</c> (<c>agent-scroll-pages.test.ts</c>): scrolling to text reads each
/// page once when it landed, and keeps reading when it may not have. Its nested describes are
/// <see cref="WithAClockTickingOnTheScreenTests"/>, <see cref="WithACounterChangingOnTheScreenTests"/>, and
/// <see cref="WithRowsThatDifferByAClockLikeValueAloneTests"/>. The scene is a <see cref="E2E.Engine.DocumentEngine"/> where
/// upstream's is a fake device engine.
/// </summary>
public sealed class ScrollToATextReadsPerPageTests
{
    [Fact]
    public async Task Reads_each_page_once_when_the_swipe_landed()
    {
        var result = await ScrollToAsync("Row 13", Turning.Now);
        Assert.Null(result.Error);
        Assert.Contains("Row 13", result.Found, StringComparison.Ordinal);
        Assert.Equal(4, result.Swipes);
        Assert.Equal([1, 1, 1, 1], result.CapturesPerPage);
    }

    [Fact]
    public async Task Keeps_reading_a_page_that_turns_after_the_stability_poll_instead_of_ending_the_list()
    {
        var result = await ScrollToAsync("Row 7", Turning.Deferred);
        Assert.Null(result.Error);
        Assert.Contains("Row 7", result.Found, StringComparison.Ordinal);
        Assert.Equal(2, result.Swipes);
    }

    [Fact]
    public async Task Keeps_reading_a_page_the_swipe_had_not_turned_yet_and_does_not_page_past_it()
    {
        var result = await ScrollToAsync("Row 7", Turning.Late);
        Assert.Null(result.Error);
        Assert.Contains("Row 7", result.Found, StringComparison.Ordinal);
        Assert.Equal(2, result.Swipes);
        Assert.All(result.CapturesPerPage, captures => Assert.True(captures >= 2));
    }

    [Fact]
    public async Task Keeps_reading_a_windowed_list_that_draws_its_rows_after_the_scroll()
    {
        var result = await ScrollToAsync("Row 4", Turning.DrawnLate);
        Assert.Null(result.Error);
        Assert.Contains("Row 4", result.Found, StringComparison.Ordinal);
        Assert.Equal(1, result.Swipes);
        Assert.True(result.CapturesPerPage[0] >= 2);
    }

    [Fact]
    public async Task Still_ends_a_list_that_stops_moving_with_LOCATOR_NOT_FOUND()
    {
        var result = await ScrollToAsync("Row 99", Turning.Never);
        Assert.Equal("LOCATOR_NOT_FOUND", result.Error);
        Assert.Equal(3, result.Swipes);
    }
}
