// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.FixtureRuntime;

/// <summary>
/// .NET-only: no upstream <c>it</c> takes <c>app.screenshot</c> on the web engine; upstream's covering tests use a fake engine
/// (<c>fixture-runtime.test.ts</c>, <c>open-session.test.ts</c>) or a device (<c>controls.e2e.ts</c>).
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class WebAppScreenshotTests
{
    [Fact]
    public async Task Writes_a_numbered_png_under_screenshots_and_returns_its_absolute_path()
    {
        using var site = await FixtureApp.StartAsync();
        var results = Path.Combine(Path.GetTempPath(), "e2e-web-shot-" + Guid.NewGuid().ToString("N"));
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(new WebEngineOptions { Headless = true }),
            BaseUrl = site.Url,
            TestTitle = "web screenshot",
            ResultsDirectory = results,
            CacheEnabled = false,
            TestTimeout = TimeSpan.FromSeconds(30),
        });
        try
        {
            await session.App.OpenAsync("/login");
            var path = await session.App.ScreenshotAsync("login page");

            Assert.True(Path.IsPathRooted(path));
            Assert.Matches(new Regex(@"[\\/]screenshots[\\/]001-login_page\.png\z", RegexOptions.None, TimeSpan.FromSeconds(2)), path);
            Assert.Equal([0x89, 0x50, 0x4E, 0x47], (await File.ReadAllBytesAsync(path))[..4]);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        }
        finally
        {
            session.Complete();
            await session.DisposeAsync();
            Directory.Delete(results, recursive: true);
        }
    }
}
