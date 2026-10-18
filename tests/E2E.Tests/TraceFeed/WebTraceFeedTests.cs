// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.TraceFeed;

/// <summary>
/// .NET-only: the web engine hands its trace the screen an action left and what the attempt runs on. Upstream's
/// <c>TraceFeed</c> (<c>web/src/trace-feed.ts</c>) is covered there only through a fake engine (<c>tests/integration/traces.test.ts</c>).
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class WebTraceFeedTests
{
    private const string Page = """
        <!DOCTYPE html>
        <html><body>
        <button type="button" onclick="document.querySelector('[role=status]').textContent = 'Saved'">Save</button>
        <div role="status">Idle</div>
        </body></html>
        """;

    [Fact]
    public async Task Tells_the_screen_an_action_left_without_retiring_the_refs_the_test_holds()
    {
        using var site = await TinySite.StartAsync(Page);
        var screens = new List<Observation>();
        await using var session = await new WebEngine(headless: true).StartAsync(
            new EngineStartOptions { BaseUrl = site.Url, Screen = screens.Add },
            CancellationToken.None);
        await session.OpenAsync(site.Url, CancellationToken.None);
        var seen = await session.ObserveAsync(CancellationToken.None);
        var save = Flatten(seen).Single(node => node.Name == "Save");
        screens.Clear();

        await session.PerformAsync(save, new LocatorAction.Tap(), CancellationToken.None);

        var left = Assert.Single(screens);
        Assert.Contains(Flatten(left), node => node.Role == "button" && node.Name == "Save");
        Assert.Contains(Flatten(left), node => node.Text == "Saved");

        // The ref from the observation before the screen was taken still names the button.
        await session.PerformAsync(save, new LocatorAction.Tap(), CancellationToken.None);
        Assert.Equal(2, screens.Count);
    }

    [Fact]
    public async Task Leaves_the_pages_element_map_as_the_last_observation_set_it()
    {
        using var site = await TinySite.StartAsync(Page);
        var screens = new List<Observation>();
        await using var session = await new WebEngine(headless: true).StartAsync(
            new EngineStartOptions { BaseUrl = site.Url, Screen = screens.Add },
            CancellationToken.None);
        await session.OpenAsync(site.Url, CancellationToken.None);
        await session.ObserveAsync(CancellationToken.None);
        var page = WebEngine.SurfaceOf(session)!.Page();
        await page.EvaluateAsync("window.__map = window[Symbol.for('e2e.observation.elements')]");

        screens.Clear();

        // A key press reads no tree of its own, so any new map would be the trace's.
        await session.PressAsync("Tab", CancellationToken.None);

        Assert.Single(screens);
        Assert.True(await page.EvaluateAsync<bool>("window.__map === window[Symbol.for('e2e.observation.elements')]"));
    }

    [Fact]
    public async Task Acts_as_before_when_the_attempt_gave_no_sink()
    {
        using var site = await TinySite.StartAsync(Page);
        await using var session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions { BaseUrl = site.Url }, CancellationToken.None);
        await session.OpenAsync(site.Url, CancellationToken.None);
        var save = Flatten(await session.ObserveAsync(CancellationToken.None)).Single(node => node.Name == "Save");

        await session.PerformAsync(save, new LocatorAction.Tap(), CancellationToken.None);
    }

    [Fact]
    public async Task Tells_the_browser_and_the_user_agent_once_after_the_first_navigation()
    {
        using var site = await TinySite.StartAsync(Page);
        var told = new List<IReadOnlyDictionary<string, string>>();
        await using var session = await new WebEngine(headless: true).StartAsync(
            new EngineStartOptions { BaseUrl = site.Url, Environment = told.Add },
            CancellationToken.None);

        await session.OpenAsync(site.Url, CancellationToken.None);
        await WaitUntilAsync(() => told.Count > 0);
        await session.OpenAsync(site.Url, CancellationToken.None);
        await Task.Delay(300);

        var facts = Assert.Single(told);
        Assert.StartsWith("chromium ", facts["browser"], StringComparison.Ordinal);
        Assert.Contains("Chrome", facts["user agent"], StringComparison.Ordinal);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }

    private static IEnumerable<SemanticNode> Flatten(Observation observation)
    {
        var pending = new Stack<SemanticNode>(observation.Roots.Reverse());
        while (pending.TryPop(out var node))
        {
            yield return node;
            foreach (var child in node.Children.Reverse())
            {
                pending.Push(child);
            }
        }
    }
}
