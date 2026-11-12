// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ClosedShadowLocators;

/// <summary>Upstream's <c>closed-shadow-locators.test.ts</c> (<c>packages/web/tests/integration</c>), the <c>closed-order</c> case.</summary>
[Collection(BrowserCollection.Name)]
public sealed class MatchOrderAcrossOneClosedRootTests
{
    [Fact]
    public async Task Lists_light_DOM_matches_first_and_the_closed_root_after_where_the_observed_tree_lists_them_in_place()
    {
        using var site = await FixtureApp.StartAsync();
        await using (var engine = await WebSemanticsTests.OpenAsync(site.Url + "closed-order", new WebEngineOptions { Headless = true }))
        {
            // The host comes before the light-DOM button, so the reader reads Alpha first.
            var observed = WebSemanticsTests.Flatten((await engine.ObserveAsync(CancellationToken.None)).Roots).Where(node => node.Role == "button");
            Assert.Equal(["Alpha", "Beta"], observed.Select(node => node.Name));
        }

        var session = await WebSemanticsTests.StartSessionAsync(site.Url);
        await WebSemanticsTests.RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/closed-order");
            // A locator searches the light DOM as one root and the closed root as the next.
            var buttons = session.Screen.GetByRole("button");
            Assert.Equal(["Beta", "Alpha"], (await buttons.ResolveAsync(CancellationToken.None)).Select(node => node.Name));
            Assert.Equal(["Beta"], (await buttons.First().ResolveAsync(CancellationToken.None)).Select(node => node.Name));
        });
    }
}
