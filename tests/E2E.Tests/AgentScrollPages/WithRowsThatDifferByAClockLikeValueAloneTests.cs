// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.AgentScrollPages.ScrollPagesKit;

namespace E2E.Tests.AgentScrollPages;

/// <summary>Upstream's <c>describe('with rows that differ by a clock-like value alone')</c>, nested in <c>describe('scroll_to a text, reads per page')</c> (<c>agent-scroll-pages.test.ts</c>).</summary>
public sealed class WithRowsThatDifferByAClockLikeValueAloneTests
{
    [Fact]
    public async Task Pages_on_to_the_row_and_reads_each_page_until_it_holds_still()
    {
        var result = await ScrollToAsync("15:30", Turning.Now, withClock: false, SlotAt);
        Assert.Null(result.Error);
        Assert.Contains("15:30", result.Found, StringComparison.Ordinal);
        Assert.Equal(4, result.Swipes);
    }

    [Fact]
    public async Task Pages_on_to_the_row_with_a_clock_ticking_beside_the_list()
    {
        var result = await ScrollToAsync("15:30", Turning.Now, withClock: true, SlotAt);
        Assert.Null(result.Error);
        Assert.Contains("15:30", result.Found, StringComparison.Ordinal);
        Assert.Equal(4, result.Swipes);
    }

    [Fact]
    public async Task Still_ends_at_the_end_of_the_list_with_LOCATOR_NOT_FOUND()
    {
        var result = await ScrollToAsync("23:00", Turning.Never, withClock: false, SlotAt);
        Assert.Equal("LOCATOR_NOT_FOUND", result.Error);
        Assert.Equal(3, result.Swipes);
    }
}
