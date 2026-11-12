// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using E2E.Tests.AgentActPixelTools;

namespace E2E.Tests.AgentObservation;

/// <summary>
/// The <c>it</c> outside any <c>describe</c> of upstream's <c>agent-observation.test.ts</c> that tester-army/e2e#1568 added. The port's feed
/// takes pixels with a screenshot of its own after the look, where upstream's engine answers a look with them, so "the capture asks for no
/// pixels" reads as the screenshot never taken. Not ported: the projection of a raw observation that carries pixels the step did not ask for.
/// </summary>
public sealed class AgentObservationTests
{
    private static async Task<(ObservationFeed Feed, PixelsEngine Engine)> FeedAsync(bool pixelsAllowed)
    {
        var engine = new PixelsEngine(new DocumentEngine(CounterPage.World()));
        var session = await engine.StartAsync(new EngineStartOptions { BaseUrl = "https://billing.test" }, CancellationToken.None);
        await session.OpenAsync("https://billing.test/", CancellationToken.None);
        var scope = new AttemptScope
        {
            Session = session,
            Agents = new Dictionary<string, ResolvedAgent>(),
            Cache = null,
            CacheEnabled = false,
            TestTitle = "feed",
            EnginePlatform = engine.Platform,
            EngineCapabilities = engine.Capabilities,
        };
        var feed = new ObservationFeed(session, TimeProvider.System)
        {
            Scope = scope,
            Options = new ObservationFeedOptions(4_096, 10_000, 0, 0, 0, PixelsAllowed: pixelsAllowed),
        };
        return (feed, engine);
    }

    [Fact]
    public async Task Enforces_tree_only_observation_requests_projection_and_unavailable_tree_fallback()
    {
        var (feed, engine) = await FeedAsync(pixelsAllowed: false);

        var view = await feed.ObserveAsync(new ExecutorObserveOptions { Pixels = true }, CancellationToken.None);

        Assert.False(engine.Looks.Last()?.PixelFallback);
        Assert.Equal(0, engine.Shots);
        Assert.Null(view.Pixels);
        Assert.Null(view.PixelsWithheld);

        (engine.TreeUnavailable, engine.IgnoresPixelFallback) = (true, true);
        var denied = await Assert.ThrowsAsync<TestException>(() => feed.ObserveAsync(null, CancellationToken.None));
        Assert.Equal("POLICY_DENIED", denied.Code);
    }
}
