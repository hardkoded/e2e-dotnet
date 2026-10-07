// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.WebPlatform;

[Collection(BrowserCollection.Name)]
public sealed class WebPlatformIntegrationTests
{
    [Fact]
    public async Task Denies_a_wrapped_or_non_http_s_scheme_on_app_open_and_browser_goto_before_it_loads()
    {
        // A local file the wrapped schemes would load. A navigation that went through would show its text.
        var directory = Directory.CreateTempSubdirectory("e2e-schemes");
        var markerPath = Path.Combine(directory.FullName, "marker.txt");
        await File.WriteAllTextAsync(markerPath, "marker-local-file\n");
        var marker = new Uri(markerPath).AbsoluteUri;
        using var site = await TinySite.StartAsync("<!DOCTYPE html><html><body><h1>Home</h1></body></html>");
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            Model = new ScriptedModel(_ => ModelResponses.Done("passed", "unused")),
            BaseUrl = site.Url,
            TestTitle = "web > schemes",
        });

        // The browser.goto steps are not ported: the port has no goto.
        foreach (var url in new[] { "view-source:" + marker, "VIEW-SOURCE:" + marker, "  view-source:" + marker, "blob:http://127.0.0.1/x", "about:srcdoc" })
        {
            await session.App.OpenAsync();
            var denied = await Assert.ThrowsAsync<TestException>(() => session.App.OpenAsync(url));
            Assert.Equal("POLICY_DENIED", denied.Code);
            Assert.Matches("^Forbidden URL scheme: (view-source|blob|about):$", denied.Message);
            Assert.DoesNotContain("marker-local-file", await session.Browser.EvaluateAsync<string>("() => document.body?.innerText ?? ''"));
        }

        await session.App.OpenAsync();
        await session.App.OpenAsync("about:blank");
        Assert.Equal("about:blank", await session.Browser.UrlAsync());

        await session.App.OpenAsync();
        var cookie = await Assert.ThrowsAsync<TestException>(
            () => session.Browser.SetCookiesAsync([new BrowserCookie { Name = "flavor", Value = "oatmeal", Url = "about:blank" }]));
        Assert.Equal("POLICY_DENIED", cookie.Code);
        Assert.Equal("Cookie URL must be http(s): about:blank", cookie.Message);
        session.Complete();
        directory.Delete(recursive: true);
    }
}
