// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.Media;

/// <summary>
/// Media emulation in a real browser: <c>ColorScheme</c> and <c>ReducedMotion</c> in the options and
/// <c>Browser.EmulateMediaAsync</c> reach the page's own <c>@media</c> rules, not only <c>matchMedia</c>,
/// from the first paint of every page the attempt opens, through <c>App.RestartAsync</c> and
/// <c>App.ClearStateAsync</c>. Driven through the engine session and the <c>Browser</c>, as the attempt drives them.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class MediaEmulationTests
{
    private const string Light = "rgb(0, 0, 0)";
    private const string Dark = "rgb(255, 255, 255)";

    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task Loads_every_page_with_the_configured_media_after_a_restart_and_a_context_reset_too()
    {
        using var app = await FixtureApp.StartAsync();
        await using var session = await StartAsync(app, new WebEngineOptions { Headless = true, ColorScheme = WebColorScheme.Dark, ReducedMotion = WebReducedMotion.Reduce });
        var expected = LoadedWith(dark: true, reduce: true);
        Assert.Equal(expected, await OpenMediaAsync(session, app));
        await session.RestartAsync(None);
        Assert.Equal(expected, await OpenMediaAsync(session, app));
        await session.ClearStateAsync(None);
        Assert.Equal(expected, await OpenMediaAsync(session, app));
    }

    [Fact]
    public async Task Leaves_the_browsers_own_media_when_none_is_configured()
    {
        using var app = await FixtureApp.StartAsync();
        await using var session = await StartAsync(app, new WebEngineOptions { Headless = true });
        Assert.Equal(LoadedWith(dark: false, reduce: false), await OpenMediaAsync(session, app));
    }

    [Fact]
    public async Task Emulates_media_set_before_the_first_navigation_from_first_paint_and_holds_a_change_for_the_rest_of_the_attempt()
    {
        using var app = await FixtureApp.StartAsync();
        await using var session = await StartAsync(app, new WebEngineOptions { Headless = true, ColorScheme = WebColorScheme.Dark });
        var browser = BrowserOf(session, app);
        await browser.EmulateMediaAsync(new WebMedia { ReducedMotion = WebReducedMotion.Reduce });
        Assert.Equal(LoadedWith(dark: true, reduce: true), await OpenMediaAsync(session, app));

        // A change applies to the open page at once; a feature left out keeps its value.
        await browser.EmulateMediaAsync(new WebMedia { ColorScheme = WebColorScheme.Light });
        Assert.Equal(LoadedWith(dark: false, reduce: true) with { AtLoad = new MediaFeatures(Dark: true, Reduce: true) }, await ReadAsync(session));
        var expected = LoadedWith(dark: false, reduce: true);
        await session.RestartAsync(None);
        Assert.Equal(expected, await OpenMediaAsync(session, app));
        await session.ClearStateAsync(None);
        Assert.Equal(expected, await OpenMediaAsync(session, app));
        await browser.EmulateMediaAsync(new WebMedia { ColorScheme = WebColorScheme.NoPreference, ReducedMotion = WebReducedMotion.NoPreference });
        var now = await ReadAsync(session);
        Assert.Equal((Light, "0.3s", false, false), (now.Color, now.Transition, now.Dark, now.Reduce));
    }

    [Fact]
    public async Task Paints_a_tab_the_app_opens_itself_with_the_configured_media_then_gives_it_a_tests_change_as_it_appears_and_later()
    {
        using var app = await FixtureApp.StartAsync();
        await using var session = await StartAsync(app, new WebEngineOptions { Headless = true, ColorScheme = WebColorScheme.Dark });
        var browser = BrowserOf(session, app);
        await browser.EmulateMediaAsync(new WebMedia { ColorScheme = WebColorScheme.Light });
        Assert.Equal(LoadedWith(dark: false, reduce: false), await OpenMediaAsync(session, app));
        var live = WebEngine.SurfaceOf(session)!;
        var popup = await live.Context().RunAndWaitForPageAsync(() => live.Page().EvaluateAsync("() => { window.open('/media'); }"));
        await popup.WaitForLoadStateAsync();
        Task<MediaFeatures> Now() => EvaluateAsync<MediaFeatures>(popup, "() => ({ dark: matchMedia('(prefers-color-scheme: dark)').matches, reduce: matchMedia('(prefers-reduced-motion: reduce)').matches })");
        Assert.Equal(new MediaFeatures(Dark: false, Reduce: false), await Now());

        // The context option paints it; a test's change arrives once the page exists, as no context-wide emulation exists.
        Assert.Equal(new MediaFeatures(Dark: true, Reduce: false), await EvaluateAsync<MediaFeatures>(popup, "() => window.atLoad"));
        await browser.EmulateMediaAsync(new WebMedia { ReducedMotion = WebReducedMotion.Reduce });
        Assert.Equal(new MediaFeatures(Dark: false, Reduce: true), await Now());
        var opener = await ReadAsync(session);
        Assert.Equal((false, true), (opener.Dark, opener.Reduce));
    }

    /// <summary>Partial: a .NET enum takes no feature or value outside its members, so only a cast value and a null media are refused.</summary>
    [Fact]
    public async Task Refuses_a_value_outside_the_ones_it_takes_and_a_null_media_changing_nothing()
    {
        using var app = await FixtureApp.StartAsync();
        await using var session = await StartAsync(app, new WebEngineOptions { Headless = true, ColorScheme = WebColorScheme.Dark });
        var browser = BrowserOf(session, app);
        var scheme = await Assert.ThrowsAsync<TestException>(() => browser.EmulateMediaAsync(new WebMedia { ColorScheme = (WebColorScheme)42 }));
        Assert.Equal("INVALID_ARGUMENT", scheme.Code);
        Assert.Equal("browser.emulateMedia({ ColorScheme }) must be Light, Dark, or NoPreference, got 42", scheme.Message);
        var motion = await Assert.ThrowsAsync<TestException>(() => browser.EmulateMediaAsync(new WebMedia { ReducedMotion = (WebReducedMotion)42 }));
        Assert.Equal("browser.emulateMedia({ ReducedMotion }) must be Reduce or NoPreference, got 42", motion.Message);
        await Assert.ThrowsAsync<ArgumentNullException>(() => browser.EmulateMediaAsync(null!));
        Assert.Equal(LoadedWith(dark: true, reduce: false), await OpenMediaAsync(session, app));
    }

    private static Browser BrowserOf(IEngineSession session, TinySite app) =>
        new(session, "web", app.Url, Directory.GetCurrentDirectory(), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1), () => CancellationToken.None, () => { }, new SoftFailures());

    private static Task<IEngineSession> StartAsync(TinySite app, WebEngineOptions options) =>
        new WebEngine(options).StartAsync(new EngineStartOptions { BaseUrl = app.Url, ActionTimeout = TimeSpan.FromSeconds(10) }, None);

    private static async Task<MediaReading> OpenMediaAsync(IEngineSession session, TinySite app)
    {
        await session.OpenAsync(app.Url + "media", None);
        return await ReadAsync(session);
    }

    // What the media page shows: its paragraph's computed style, what matchMedia reports now, and what its head script read before first paint.
    private static Task<MediaReading> ReadAsync(IEngineSession session) =>
        EvaluateAsync<MediaReading>(WebEngine.SurfaceOf(session)!.Page(), """
            () => {
              const style = getComputedStyle(document.querySelector('p'));
              return {
                color: style.color,
                transition: style.transitionDuration,
                dark: matchMedia('(prefers-color-scheme: dark)').matches,
                reduce: matchMedia('(prefers-reduced-motion: reduce)').matches,
                atLoad: window.atLoad,
              };
            }
            """);

    // Playwright cannot build a positional record, so the page's object comes back as JSON.
    private static async Task<T> EvaluateAsync<T>(IPage page, string expression) =>
        (await page.EvaluateAsync<JsonElement>(expression)).Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    // The reading of a page that loaded with the media in force.
    private static MediaReading LoadedWith(bool dark, bool reduce) =>
        new(dark ? Dark : Light, reduce ? "0s" : "0.3s", dark, reduce, new MediaFeatures(dark, reduce));

    private sealed record MediaReading(string Color, string Transition, bool Dark, bool Reduce, MediaFeatures AtLoad);

    private sealed record MediaFeatures(bool Dark, bool Reduce);
}
