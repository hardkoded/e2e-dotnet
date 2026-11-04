// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ConcurrentObserve;

/// <summary>
/// Upstream's <c>packages/web/tests/integration/concurrent-observe.test.ts</c> (tester-army/e2e#1544, <c>8f17ad3</c>), which has one <c>it</c>
/// outside any <c>describe</c>: two observations at once take turns, and neither supersedes the other's capture mid-flight.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ConcurrentObserveTests
{
    /// <summary>
    /// Partial: upstream's observations also carry <c>pixels</c> and the test checks both are defined. The port's <c>ObserveAsync</c> returns
    /// the tree alone (the image is <c>ScreenshotAsync</c>), so this test checks the two trees and that the last one's refs act.
    /// </summary>
    [Fact]
    public async Task Partial_Captures_both_of_two_concurrent_observations_and_the_refs_of_the_last_stay_actionable()
    {
        await using var session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions(), CancellationToken.None);
        await session.OpenAsync("about:blank", CancellationToken.None);
        var page = WebEngine.SurfaceOf(session)!.Page();
        await page.SetContentAsync("<button onclick=\"document.body.dataset.clicked = 'yes'\">Press</button>");

        var observed = await Task.WhenAll(session.ObserveAsync(CancellationToken.None), session.ObserveAsync(CancellationToken.None));

        Assert.All(observed, observation => Assert.Contains(Flatten(observation.Roots), node => node.Role == "button"));
        var button = Flatten(observed[1].Roots).First(node => node.Role == "button");
        await session.PerformAsync(button, new LocatorAction.Tap(), CancellationToken.None);
        Assert.Equal("yes", await page.EvaluateAsync<string>("() => document.body.dataset.clicked"));
    }

    private static IEnumerable<SemanticNode> Flatten(IEnumerable<SemanticNode> nodes) => nodes.SelectMany(node => (IEnumerable<SemanticNode>)[node, .. Flatten(node.Children)]);
}
