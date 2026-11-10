// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ReadNodeNames;

/// <remarks>
/// Not ported: upstream's "reports an on-site frame past the depth limit as truncation" (the port's walk has no frame depth limit;
/// issue #1220).
/// Partial: <c>Partial_Does_not_count_an_off_site_frame...</c> checks the frame is left out and not truncated, with the port's real
/// budget filled by the page (the port's observation has no budget argument), and not upstream's exact node count of 3, which the
/// observation does not expose.
/// </remarks>
[Collection(BrowserCollection.Name)]
public sealed class TruncatedFlagTests(ChromiumPage chromium) : IClassFixture<ChromiumPage>
{
    [Fact]
    public async Task Reports_a_walk_the_node_budget_stopped_and_a_whole_read_as_not_truncated()
    {
        var buttons = string.Concat(Enumerable.Range(0, 20).Select(index => $"<button>Button {index}</button>"));
        await chromium.Page.SetContentAsync($"<main>{buttons}</main>");

        await chromium.CaptureAsync(100);
        Assert.False(chromium.Truncated);
        Assert.Equal(21, chromium.NodeCount);

        await chromium.CaptureAsync(5);
        Assert.True(chromium.Truncated);
        Assert.Equal(5, chromium.NodeCount);
    }

    [Fact]
    public async Task Partial_Does_not_count_an_off_site_frame_it_leaves_out_by_design_even_with_no_budget_left()
    {
        // With the app on one site, a data: document is never entered: it has an opaque origin, not the app's.
        const string Ad = "<button>One</button><iframe src=\"data:text/html,<button>Ad</button>\"></iframe>";
        var filler = string.Concat(Enumerable.Repeat("<button>Fill</button>", ObservationLimits.Nodes - 2));

        using var whole = await TinySite.StartAsync($"<!DOCTYPE html><html><body>{Ad}</body></html>");
        await using (var session = await WebSemanticsTests.OpenAsync(whole.Url, new WebEngineOptions { Headless = true }))
        {
            var observation = await session.ObserveAsync(CancellationToken.None);
            Assert.False(observation.Truncated);
            Assert.DoesNotContain(WebSemanticsTests.Flatten(observation.Roots), node => node.Name == "Ad");
        }

        // The page lists exactly the budget in nodes: the frame's button is the one a walk that entered the frame would be cut short of.
        using var cut = await TinySite.StartAsync($"<!DOCTYPE html><html><body>{Ad}{filler}</body></html>");
        await using (var session = await WebSemanticsTests.OpenAsync(cut.Url, new WebEngineOptions { Headless = true }))
        {
            var observation = await session.ObserveAsync(CancellationToken.None);
            Assert.False(observation.Truncated);
            Assert.DoesNotContain(WebSemanticsTests.Flatten(observation.Roots), node => node.Name == "Ad");
        }
    }

    [Fact]
    public async Task Reports_a_child_frame_the_budget_could_not_enter()
    {
        // The page lists exactly the budget in nodes: the button, the frame, and the buttons that fill the rest.
        var filler = string.Concat(Enumerable.Repeat("<button>Fill</button>", ObservationLimits.Nodes - 2));
        const string Frame = "<button>One</button><iframe title=\"Frame\" srcdoc=\"<button>Inside</button>\"></iframe>";

        using var whole = await TinySite.StartAsync($"<!DOCTYPE html><html><body>{Frame}</body></html>");
        await using (var session = await WebSemanticsTests.OpenAsync(whole.Url, new WebEngineOptions { Headless = true }))
        {
            var observation = await session.ObserveAsync(CancellationToken.None);
            Assert.False(observation.Truncated);
            Assert.Contains(WebSemanticsTests.Flatten(observation.Roots), node => node.Name == "Inside");
        }

        using var cut = await TinySite.StartAsync($"<!DOCTYPE html><html><body>{Frame}{filler}</body></html>");
        await using (var session = await WebSemanticsTests.OpenAsync(cut.Url, new WebEngineOptions { Headless = true }))
        {
            var observation = await session.ObserveAsync(CancellationToken.None);
            Assert.True(observation.Truncated);
            Assert.DoesNotContain(WebSemanticsTests.Flatten(observation.Roots), node => node.Name == "Inside");
        }
    }
}
