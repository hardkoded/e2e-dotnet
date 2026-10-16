// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.TestMu.Tests.Live;

/// <summary>Runs only when LT_USERNAME and LT_ACCESS_KEY are set: a skipped test, not a failure, without them.</summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LT_USERNAME")) || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LT_ACCESS_KEY")))
        {
            Skip = "LT_USERNAME and LT_ACCESS_KEY are not set.";
        }
    }
}

/// <summary>
/// Against the real TestMu grid. This is the check of the one thing the unit tests cannot: that the grid accepts a CDP
/// attach on its Playwright URL. .NET-only.
/// </summary>
[Trait("TestCategory", "TestMu")]
public sealed class TestMuLiveTests
{
    [LiveFact]
    public async Task Attaches_over_cdp_and_opens_a_page()
    {
        var provider = new TestMuBrowsers();
        var env = new Dictionary<string, string?>
        {
            ["LT_USERNAME"] = Environment.GetEnvironmentVariable("LT_USERNAME"),
            ["LT_ACCESS_KEY"] = Environment.GetEnvironmentVariable("LT_ACCESS_KEY"),
        };
        var lease = await provider.AcquireAsync(new BrowserRequest { RunId = "live", TargetName = "web", Slot = 0, Slots = 1, Env = env, Log = _ => { } }, CancellationToken.None);

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.ConnectOverCDPAsync(lease.CdpEndpoint);
        try
        {
            var page = browser.Contexts.FirstOrDefault()?.Pages.FirstOrDefault() ?? await (browser.Contexts.FirstOrDefault() ?? await browser.NewContextAsync()).NewPageAsync();
            await page.GotoAsync("https://example.com");
            Assert.Equal("Example Domain", await page.TitleAsync());
        }
        finally
        {
            await browser.CloseAsync();
            await provider.ReleaseAsync(lease, new BrowserReleaseContext { RunId = "live", TargetName = "web", Env = env, Log = _ => { } }, CancellationToken.None);
        }
    }
}
