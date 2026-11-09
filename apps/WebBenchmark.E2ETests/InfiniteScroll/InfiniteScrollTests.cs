// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.InfiniteScroll;

/// <summary>Ported from upstream's <c>apps/web-benchmark/tests/infinite-scroll.e2e.ts</c>: its one test is outside any <c>describe</c>.</summary>
public sealed class InfiniteScrollTests : BenchmarkTest
{
    [Test]
    public async Task Scrolls_the_feed_until_the_Golden_Ticket_loads_then_claims_it()
    {
        await App.OpenAsync("/e/infinite-scroll");
        var feed = Screen.GetByTestId("feed");
        await Expect.That(feed.GetByTestId("feed-item")).ToHaveCountAsync(20);
        // The feed is its own scroll container, so the feed pages, not the viewport.
        // The Golden Ticket is item 138: 137 rows of 61 px at a 360 px slow stride
        // and seven lazy loads of 400 ms, about 4 s measured, so the default 30 s
        // scroll budget holds seven of those.
        var claim = Screen.GetByRole("button", "Claim");
        await feed.ScrollUntilVisibleAsync(claim);
        await Expect.That(feed.GetByText("Golden Ticket")).ToBeVisibleAsync();
        await claim.TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Golden Ticket claimed");
    }
}
