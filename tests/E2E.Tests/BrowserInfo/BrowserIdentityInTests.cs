// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using Microsoft.Playwright;
using static E2E.Tests.BrowserInfo.BrowserInfoHarness;

namespace E2E.Tests.BrowserInfo;

/// <summary>
/// <c>describe.each(browsers)('browser identity in $name')</c> of upstream's <c>packages/web/tests/integration/browser-info.test.ts</c>
/// (tester-army/e2e#2251). Firefox and WebKit run where Playwright's builds are installed; without one the test returns at once, as
/// upstream's <c>it.skipIf(!installed)</c> skips it.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed partial class BrowserIdentityInTests
{
    [Theory]
    [InlineData(BrowserName.Chromium)]
    [InlineData(BrowserName.Firefox)]
    [InlineData(BrowserName.Webkit)]
    public async Task Names_the_engine_and_reports_the_version_the_page_itself_reports(BrowserName name)
    {
        if (!await InstalledAsync(name))
        {
            return;
        }

        using var app = await FixtureApp.StartAsync();
        await WithBrowserAsync(new WebEngineOptions { BrowserName = name }, app, async (browser, _) =>
        {
            // Before any page opens: a test skips on the browser first.
            Assert.Equal(name, browser.Name);
            Assert.Matches(VersionPattern(), browser.Version);
            await browser.GotoAsync("/");
            Assert.Contains(InPage(name, browser.Version), await PageVersionAsync(browser));
        });
    }

    [Theory]
    [InlineData(BrowserName.Chromium)]
    [InlineData(BrowserName.Firefox)]
    [InlineData(BrowserName.Webkit)]
    public async Task Keeps_the_real_version_when_the_target_overrides_the_user_agent(BrowserName name)
    {
        if (!await InstalledAsync(name))
        {
            return;
        }

        using var app = await FixtureApp.StartAsync();
        var real = "";
        await WithBrowserAsync(new WebEngineOptions { BrowserName = name }, app, (browser, _) =>
        {
            real = browser.Version;
            return Task.CompletedTask;
        });
        await WithBrowserAsync(
            new WebEngineOptions { BrowserName = name, UserAgent = "Custom/1.0" },
            app,
            async (browser, _) =>
            {
                await browser.GotoAsync("/");
                Assert.Equal("Custom/1.0", await browser.EvaluateAsync<string>("() => navigator.userAgent"));
                Assert.Equal(real, browser.Version);
                Assert.Equal(name, browser.Name);
            }
        );
    }

    [GeneratedRegex(@"^\d+(\.\d+)+\z")]
    private static partial Regex VersionPattern();

    private static async Task<bool> InstalledAsync(BrowserName name)
    {
        using var playwright = await Playwright.CreateAsync();
        return name switch
        {
            BrowserName.Firefox => File.Exists(playwright.Firefox.ExecutablePath),
            BrowserName.Webkit => File.Exists(playwright.Webkit.ExecutablePath),
            _ => true,
        };
    }

    private static string InPage(BrowserName name, string version) =>
        name switch
        {
            BrowserName.Firefox => $"Firefox/{version.Split('.')[0]}.",
            BrowserName.Webkit => $"Version/{version}",
            _ => $"/{version}",
        };
}
