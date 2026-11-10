// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.Engine;

namespace E2E.Tests.ReadNodeNames;

/// <remarks>
/// Not ported: upstream's "does not count an off-site frame it leaves out by design, even with no budget left" (the
/// port's web engine reads every iframe's document, off-site or not, so no frame is left out by design; issue #1224).
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

    [Fact]
    public async Task Reports_an_on_site_frame_past_the_depth_limit_as_truncation()
    {
        // Nest same-origin srcdoc frames one deeper than the capture follows.
        using var within = await TinySite.StartAsync(NestedFrames(4));
        await using (var session = await WebSemanticsTests.OpenAsync(within.Url, new WebEngineOptions { Headless = true }))
        {
            var observation = await session.ObserveAsync(CancellationToken.None);
            Assert.False(observation.Truncated);
            Assert.Contains(WebSemanticsTests.Flatten(observation.Roots), node => node.Name == "Level 4");
        }

        using var beyond = await TinySite.StartAsync(NestedFrames(5));
        await using (var session = await WebSemanticsTests.OpenAsync(beyond.Url, new WebEngineOptions { Headless = true }))
        {
            var observation = await session.ObserveAsync(CancellationToken.None);
            Assert.True(observation.Truncated);
            Assert.DoesNotContain(WebSemanticsTests.Flatten(observation.Roots), node => node.Name == "Level 5");
        }
    }

    /// <summary>A page whose frames nest <paramref name="depth"/> levels deep; level N's frame holds level N+1's.</summary>
    private static string NestedFrames(int depth)
    {
        static string Level(int level, int depth) =>
            $"<button>Level {level}</button>" + (level < depth ? $"<iframe srcdoc=\"{WebUtility.HtmlEncode(Level(level + 1, depth))}\"></iframe>" : "");

        return $"<!DOCTYPE html><html><body><button>Top</button><iframe srcdoc=\"{WebUtility.HtmlEncode(Level(1, depth))}\"></iframe></body></html>";
    }
}
