// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using E2E.Engine;

namespace E2E.NUnit.Tests.Browser;

/// <summary>
/// The tests of upstream's <c>apps/testbed/tests/browser.e2e.ts</c> outside its <c>browser fixture</c>
/// <c>describe</c>, against the playground's <c>/browser</c> page.
/// </summary>
public sealed class BrowserTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Emulated_media_reaches_the_pages_own_media_rules_from_the_first_paint_on()
    {
        await Browser.EmulateMediaAsync(new WebMedia { ColorScheme = WebColorScheme.Dark });
        await App.OpenAsync("/browser");
        await Expect.That(Screen.GetByLabel("Media")).ToHaveTextAsync("dark, full motion");
        await Browser.EmulateMediaAsync(new WebMedia { ReducedMotion = WebReducedMotion.Reduce });
        await Expect.That(Screen.GetByLabel("Media")).ToHaveTextAsync("dark, reduced motion");
        await App.RestartAsync();
        await App.OpenAsync("/browser");
        await Expect.That(Screen.GetByLabel("Media")).ToHaveTextAsync("dark, reduced motion");
        await Browser.EmulateMediaAsync(new WebMedia { ColorScheme = WebColorScheme.Light, ReducedMotion = WebReducedMotion.NoPreference });
        await Expect.That(Screen.GetByLabel("Media")).ToHaveTextAsync("light, full motion");
    }
}
