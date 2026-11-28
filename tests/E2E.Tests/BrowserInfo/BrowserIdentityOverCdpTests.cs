// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using static E2E.Tests.BrowserInfo.BrowserInfoHarness;

namespace E2E.Tests.BrowserInfo;

/// <summary><c>describe('browser identity over CDP')</c> of upstream's <c>browser-info.test.ts</c>.</summary>
[Collection(BrowserCollection.Name)]
public sealed class BrowserIdentityOverCdpTests
{
    [Fact]
    public async Task Reports_the_attached_browser_by_what_its_own_DevTools_endpoint_reports()
    {
        using var app = await FixtureApp.StartAsync();
        await using var chrome = await RemoteChrome.LaunchAsync();
        var reported = await RemoteVersionAsync(chrome);
        await WithBrowserAsync(
            new WebEngineOptions
            {
                Connect = new WebConnectOptions { CdpEndpoint = _ => Task.FromResult<CdpTarget>(chrome.Endpoint) },
            },
            app,
            (browser, _) =>
            {
                Assert.Equal(BrowserName.Chromium, browser.Name);
                Assert.Equal(reported, browser.Version);
                return Task.CompletedTask;
            }
        );
    }

    /// <summary>The version the host's own <c>/json/version</c> reports (upstream's <c>reported.Browser.split('/')[1]</c>).</summary>
    internal static async Task<string> RemoteVersionAsync(RemoteChrome chrome)
    {
        using var http = new HttpClient();
        var json = await http.GetStringAsync($"http://127.0.0.1:{new Uri(chrome.Endpoint).Port}/json/version");
        return JsonDocument.Parse(json).RootElement.GetProperty("Browser").GetString()!.Split('/')[1];
    }
}
