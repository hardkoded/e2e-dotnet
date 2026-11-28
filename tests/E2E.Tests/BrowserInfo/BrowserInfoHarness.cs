// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.BrowserInfo;

/// <summary>What upstream's <c>browser-info.test.ts</c> keeps outside its <c>describe</c>s: one attempt of an engine, with the <c>browser</c> fixture.</summary>
internal static class BrowserInfoHarness
{
    private static readonly CancellationToken None = CancellationToken.None;

    /// <summary>What the page itself knows of its browser's version: the full one on Chromium, the user agent's elsewhere (upstream's <c>pageVersion</c>).</summary>
    public static Task<string?> PageVersionAsync(Browser browser) =>
        browser.EvaluateAsync<string>(
            """
            async () => {
              const data = navigator.userAgentData;
              if (data === undefined) return navigator.userAgent;
              const { fullVersionList } = await data.getHighEntropyValues(['fullVersionList']);
              return fullVersionList.map((entry) => `${entry.brand}/${entry.version}`).join(' ');
            }
            """
        );

    /// <summary>The <c>browser</c> fixture of the session; <paramref name="ended"/> is the token of that attempt's harness (upstream's <c>fixtureOf</c>).</summary>
    public static Browser FixtureOf(IEngineSession session, string baseUrl, CancellationToken ended) =>
        new(
            session,
            "web",
            baseUrl,
            Directory.GetCurrentDirectory(),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5),
            () => ended,
            () => { },
            new SoftFailures()
        );

    /// <summary>Starts an attempt of <paramref name="engine"/> (upstream's <c>startAttempt</c>).</summary>
    public static Task<IEngineSession> StartAttemptAsync(WebEngine engine, string baseUrl, string artifactsDir) =>
        engine.StartAsync(
            new EngineStartOptions
            {
                BaseUrl = baseUrl,
                ArtifactsDir = artifactsDir,
                ActionTimeout = TimeSpan.FromSeconds(30),
            },
            None
        );

    /// <summary>Runs <paramref name="body"/> in one attempt of an engine built from <paramref name="options"/>, with the app open, then disposes the engine (upstream's <c>withBrowser</c>).</summary>
    public static async Task WithBrowserAsync(
        WebEngineOptions options,
        TinySite app,
        Func<Browser, WebEngine, Task> body
    )
    {
        var artifactsDir = Directory.CreateTempSubdirectory("e2e-browser-info-").FullName;
        var engine = new WebEngine(options);
        try
        {
            await using var session = await StartAttemptAsync(engine, app.Url, artifactsDir);
            var browser = FixtureOf(session, app.Url, None);
            await body(browser, engine);
        }
        finally
        {
            await engine.DisposeAsync();
            Directory.Delete(artifactsDir, recursive: true);
        }
    }
}
