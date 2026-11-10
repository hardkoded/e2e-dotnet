// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark.E2ETests.Fixtures;

namespace WebBenchmark.E2ETests.CssContentUi;

/// <summary>Ported from <c>describe('css content ui')</c> in upstream's <c>apps/web-benchmark/tests/css-content-ui.e2e.ts</c>.</summary>
public sealed class CssContentUiTests : BenchmarkTest
{
    private const string PseudoTextScript = """
        (css) => {
          const node = document.querySelector(css);
          if (node === null) throw new Error(`no node matches ${css}`);
          return getComputedStyle(node, '::before').content.replace(/^"|"$/g, '');
        }
        """;

    [SetUp]
    public async Task OpenTheCssContentUi() => await App.OpenAsync("/e/css-content-ui");

    [Test]
    public async Task The_tile_labelled_Ship_it_advances_and_confirming_ships()
    {
        await Expect.Poll(() => PseudoText(".wb-css-instruction")).ToBeAsync("Press Ship it, then confirm");
        await Expect.Poll(() => PseudoText(".wb-css-action-b")).ToBeAsync("Ship it");

        await Browser.Locator(".wb-css-action-b").TapAsync();
        await Expect.Poll(() => PseudoText(".wb-css-confirm")).ToBeAsync("Confirm ship");

        await Browser.Locator(".wb-css-confirm").TapAsync();
        await Expect.Poll(() => PseudoText(".wb-css-success")).ToBeAsync("Shipped successfully");
    }

    [Test]
    public async Task A_wrong_tile_shows_the_CSS_rendered_error_and_keeps_the_picker()
    {
        await Browser.Locator(".wb-css-action-a").TapAsync();
        await Expect.Poll(() => PseudoText(".wb-css-error")).ToBeAsync("Wrong action");
        await Expect.That(Browser.Locator(".wb-css-action-b")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Reads the <c>::before</c> text of the first node matching <paramref name="selector"/>. The page paints every label this way and the DOM
    /// holds no text, so no locator matcher can read it; the poll over this read stands in for the CSS content matcher the engine does not have.
    /// </summary>
    private async Task<string> PseudoText(string selector) =>
        await Browser.EvaluateAsync<string>(PseudoTextScript, selector) ?? "";
}
