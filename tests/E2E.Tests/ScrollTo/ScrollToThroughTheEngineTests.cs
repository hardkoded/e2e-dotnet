// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ScrollTo;

/// <summary>
/// Upstream's <c>describe('scrollTo through the engine')</c> (<c>packages/web/tests/integration/scroll-to.test.ts</c>): the reason an endless feed
/// ran out of budget survives the engine's classification, on the screen and the locator form. The screen form is the port's
/// <see cref="IEngineSession.ScrollToAsync(ScrollEdge, OperationContext, CancellationToken)"/>, which stands for upstream's
/// <c>perform(root.ref, scrollTo)</c>: the port's engine has no node for the observation root.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ScrollToThroughTheEngineTests
{
    private static readonly string Endless = $$"""
        <!doctype html><div data-testid="feed">{{ScrollToPage.Rows}}</div>
        <script>addEventListener('scroll', () => document.body.insertAdjacentHTML('beforeend', '<p style="height: 400px">More</p>'));</script>
        """;

    [Theory]
    [InlineData("screen")]
    [InlineData("locator")]
    public async Task Keeps_the_reason_an_endless_feed_ran_out_of_budget_on_the_x_form(string form)
    {
        using var site = await TinySite.StartAsync(Endless);
        await using var session = await new WebEngine(headless: true).StartAsync(
            new EngineStartOptions(),
            CancellationToken.None
        );
        await session.OpenAsync(site.Url, CancellationToken.None);
        var operation = OperationContext.Test with { Timeout = TimeSpan.FromSeconds(1) };
        var observation = await session.ObserveAsync(CancellationToken.None);

        var failure = await Assert.ThrowsAsync<EngineException>(async () =>
        {
            if (form == "screen")
            {
                await session.ScrollToAsync(ScrollEdge.Bottom, operation, CancellationToken.None);
                return;
            }

            var feed = Assert.Single(
                await session.LocateAsync(
                    LocatorExpression.For("testid", "feed", new TextMatchOptions(), "testId"),
                    TimeSpan.FromSeconds(10),
                    CancellationToken.None
                )
            );
            await session.PerformAsync(
                feed,
                new LocatorAction.ScrollTo(ScrollEdge.Bottom),
                operation,
                CancellationToken.None
            );
        });

        Assert.NotEmpty(observation.Roots);
        Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
        Assert.Contains("more content kept loading at the bottom edge", failure.Message);
    }
}
