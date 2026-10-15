// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RouteAnnouncer;

/// <summary>Ported from upstream's <c>route-announcer.test.ts</c>: Next.js App Router's route announcer is never observed; an app's own alert still is.</summary>
[Collection(BrowserCollection.Name)]
public sealed class RouteAnnouncerTests(ChromiumPage chromium) : IClassFixture<ChromiumPage>
{
    private const string Page = """
        <!doctype html><html lang="en"><head><title>Acme Notes</title></head><body>
          <div role="alert" data-testid="app-alert">Acme Notes</div>
          <next-route-announcer></next-route-announcer>
          <script>
            document.querySelector('next-route-announcer').attachShadow({ mode: 'open' }).innerHTML =
              '<div id="__next-route-announcer__" role="alert" aria-live="assertive" style="position:absolute;width:1px;height:1px;overflow:hidden"></div>';
          </script>
        </body></html>
        """;

    [Fact]
    public async Task Omits_the_announcer_empty_or_populated_and_keeps_the_app_alert()
    {
        await chromium.Page.SetContentAsync(Page);
        var announcer = chromium.Page.Locator("next-route-announcer").Locator("#__next-route-announcer__");
        foreach (var text in new[] { "", "Acme Notes" })
        {
            await announcer.EvaluateAsync("(element, value) => { element.textContent = value; }", text);
            var alert = Assert.Single(await chromium.CaptureAsync(), node => node.Role == "alert");
            Assert.Equal("Acme Notes", alert.Name);
            Assert.Equal("app-alert", alert.TestId);
        }

        Assert.Equal("Acme Notes", await announcer.InnerTextAsync());
    }
}
