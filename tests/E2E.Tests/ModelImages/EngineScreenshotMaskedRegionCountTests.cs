// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ModelImages;

/// <summary>
/// The count of regions the web engine covers in a screenshot. These are .NET-only tests: upstream proves the count
/// through its own web surface.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class EngineScreenshotMaskedRegionCountTests
{
    private const string Page = """
        <!DOCTYPE html>
        <html><body>
        <input aria-label="Email" style="width:200px;height:40px">
        <input aria-label="Password" type="password" style="width:200px;height:40px">
        <input aria-label="Confirm" type="password" style="width:200px;height:40px">
        </body></html>
        """;

    private static async Task<EngineScreenshot> ScreenshotAsync(string html)
    {
        using var site = await TinySite.StartAsync(html);
        await using var session = await new WebEngine(new WebEngineOptions()).StartAsync(
            new EngineStartOptions { BaseUrl = site.Url, ActionTimeout = TimeSpan.FromSeconds(30) },
            CancellationToken.None);
        await session.OpenAsync(site.Url, CancellationToken.None);
        return await session.ScreenshotAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Web_counts_every_secure_field_it_covers()
    {
        Assert.Equal(2, (await ScreenshotAsync(Page)).MaskedRegionCount);
    }

    [Fact]
    public async Task Web_counts_none_for_a_page_without_a_secure_field()
    {
        Assert.Equal(0, (await ScreenshotAsync("<!DOCTYPE html><html><body><h1>Home</h1></body></html>")).MaskedRegionCount);
    }
}
