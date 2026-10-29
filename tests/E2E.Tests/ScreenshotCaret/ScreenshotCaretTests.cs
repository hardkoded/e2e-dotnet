// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using Microsoft.Playwright;
using Caret = E2E.Engine.ScreenshotCaret;

namespace E2E.Tests.ScreenshotCaret;

/// <summary>
/// A screenshot must not edit the form elements a server-rendered app hydrates (upstream
/// <c>screenshot-caret.test.ts</c>). Upstream spies on <c>page.screenshot</c> to sample the page at the moment the
/// capture is taken, and the port cannot intercept Playwright's sealed page. These tests sample inside
/// <c>Caret.WithHiddenCaretAsync</c> in its place, so each is <c>Partial_</c>: that the engine's own
/// <c>ScreenshotAsync</c> hands Playwright <c>caret: 'initial'</c> inside the helper is checked only through the
/// absence of any <c>style</c> write.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ScreenshotCaretTests
{
    private const string WatchStyleWrites = """
        () => {
          const into = [];
          window.styleMutations = into;
          new MutationObserver((records) => {
            for (const record of records) {
              if (record.type === 'attributes' && record.attributeName === 'style') into.push(record.target.id);
            }
          }).observe(document.documentElement, { subtree: true, attributes: true, attributeFilter: ['style'] });
        }
        """;

    private const string MainSample = """
        () => {
          const read = {};
          for (const id of ['plain', 'notes', 'secret']) {
            const element = document.getElementById(id);
            if (element !== null) read[id] = getComputedStyle(element).caretColor;
          }
          const tracker = globalThis[Symbol.for('e2e.closedShadowRoots')];
          const closed = tracker?.get(document.getElementById('caret-host'));
          if (closed === undefined) throw new Error('the fixture closed root was not recorded');
          const shadowed = closed.getElementById('shadowed');
          if (shadowed !== null) read.shadowed = getComputedStyle(shadowed).caretColor;
          return { colors: read, adopted: [document.adoptedStyleSheets.length, closed.adoptedStyleSheets.length] };
        }
        """;

    /// <summary>The caret color of every editable, light DOM, closed root, and iframe, and the sheets each root holds.</summary>
    private sealed record Sample(SortedDictionary<string, string> Colors, int[] Adopted)
    {
        public bool SameAs(Sample other) =>
            Colors.SequenceEqual(other.Colors) && Adopted.SequenceEqual(other.Adopted);
    }

    private static IFrame Framed(IPage page) =>
        page.Frames.FirstOrDefault(candidate => candidate != page.MainFrame) ?? throw new InvalidOperationException("the fixture iframe is missing");

    private static async Task<Sample> SampleAsync(IPage page)
    {
        var main = await page.EvaluateAsync<JsonElement>(MainSample);
        var colors = new SortedDictionary<string, string>();
        foreach (var color in main.GetProperty("colors").EnumerateObject().Where(property => property.Name != "$id"))
        {
            colors[color.Name] = color.Value.GetString()!;
        }

        var frame = Framed(page);
        colors["framed"] = await frame.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('framed')).caretColor");
        var framedAdopted = await frame.EvaluateAsync<int>("() => document.adoptedStyleSheets.length");
        var adopted = main.GetProperty("adopted").EnumerateArray().Select(count => count.GetInt32()).Append(framedAdopted).ToArray();
        return new Sample(colors, adopted);
    }

    private static async Task<(IEngineSession Session, IPage Page)> OpenAsync(string url)
    {
        var session = await new WebEngine(new WebEngineOptions()).StartAsync(
            new EngineStartOptions { BaseUrl = url, ActionTimeout = TimeSpan.FromSeconds(30) },
            CancellationToken.None);
        try
        {
            await session.OpenAsync(url, CancellationToken.None);
            return (session, WebEngine.SurfaceOf(session)!.Page());
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    private static SortedDictionary<string, string> Colors(string color, params string[] ids) =>
        new(ids.ToDictionary(id => id, _ => color));

    [Fact]
    public async Task Partial_hides_every_caret_while_the_capture_runs_and_restores_each_root_afterwards()
    {
        using var site = await FixtureApp.StartAsync();
        var (session, page) = await OpenAsync(site.Url + "caret");
        await using var _ = session;
        // The main document and the iframe document each watch their own `style` writes, so a hidden write in
        // either one fails the test.
        await page.EvaluateAsync(WatchStyleWrites);
        await Framed(page).EvaluateAsync(WatchStyleWrites);
        var before = await SampleAsync(page);
        var expectedBefore = Colors("rgb(0, 0, 0)", "plain", "notes", "secret", "framed");
        expectedBefore["shadowed"] = "rgb(255, 0, 0)";
        Assert.Equal(expectedBefore, before.Colors);
        Assert.Equal([0, 0, 0], before.Adopted);

        var during = await Caret.WithHiddenCaretAsync(page, () => SampleAsync(page));
        var capture = await session.ScreenshotAsync(CancellationToken.None);

        Assert.True(capture.MaskedRegionCount > 0);
        Assert.Empty(await page.EvaluateAsync<string[]>("() => window.styleMutations"));
        Assert.Empty(await Framed(page).EvaluateAsync<string[]>("() => window.styleMutations"));
        Assert.Equal(Colors("rgba(0, 0, 0, 0)", "plain", "notes", "secret", "shadowed", "framed"), during.Colors);
        Assert.Equal([1, 1, 1], during.Adopted);
        Assert.True((await SampleAsync(page)).SameAs(before));
    }

    [Fact]
    public async Task Partial_restores_every_root_when_the_screenshot_fails()
    {
        using var site = await FixtureApp.StartAsync();
        var (session, page) = await OpenAsync(site.Url + "caret");
        await using var _ = session;
        var before = await SampleAsync(page);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Caret.WithHiddenCaretAsync<int>(page, () => throw new InvalidOperationException("capture failed")));

        Assert.Equal("capture failed", error.Message);
        Assert.True((await SampleAsync(page)).SameAs(before));
    }

    [Fact]
    public async Task Partial_hides_the_caret_on_a_page_whose_style_src_policy_blocks_style_elements()
    {
        using var site = await FixtureApp.StartAsync();
        var (session, page) = await OpenAsync(site.Url + "caret-csp");
        await using var _ = session;
        Task<string> CaretColor() => page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('plain')).caretColor");
        // The policy is real: a style element does not apply.
        Assert.Equal("rgb(0, 0, 0)", await page.EvaluateAsync<string>("""
            () => {
              const style = document.createElement('style');
              style.textContent = 'input { caret-color: transparent !important; }';
              document.head.append(style);
              return getComputedStyle(document.getElementById('plain')).caretColor;
            }
            """));

        var during = await Caret.WithHiddenCaretAsync(page, CaretColor);
        await session.ScreenshotAsync(CancellationToken.None);

        Assert.Equal("rgba(0, 0, 0, 0)", during);
        Assert.Equal("rgb(0, 0, 0)", await CaretColor());
        Assert.Equal(0, await page.EvaluateAsync<int>("() => document.adoptedStyleSheets.length"));
    }
}
