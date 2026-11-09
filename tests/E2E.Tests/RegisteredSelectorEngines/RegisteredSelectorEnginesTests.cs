// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.RegisteredSelectorEngines;

/// <summary>
/// .NET-only (no upstream <c>it</c> runs the engines on a page: upstream's <c>closed-shadow.test.ts</c> runs <c>e2e-roots</c> against a
/// scripted document, and its integration tests reach <c>e2e-label</c>, <c>e2e-read</c> and <c>e2e-shown</c> through locators). Each engine the web engine
/// registers (<see cref="E2E.Engine.SelectorEngines"/>) is run here through a real Playwright selector on a real page, since
/// <c>LabelSelector</c> and <c>ShownSelector</c> embed the port's reader (<c>ReadSemantics.Prelude</c>), which a scripted document cannot exercise.
/// </summary>
public sealed class RegisteredSelectorEnginesTests : IAsyncLifetime
{
    private const string Body = """
        <label>Display name <span aria-hidden="true">*</span> <input id="starred"></label>
        <label>Last Name <input id="last"></label>
        <input id="aria" aria-label="Display name">
        <button id="shown">Shown</button>
        <button id="gone" style="display: none">Gone</button>
        <div style="display: none"><button id="inside-none">Inside none</button></div>
        <div style="content-visibility: hidden; height: 20px"><button id="inside-skipped">Inside skipped</button></div>
        <button id="invisible" style="visibility: hidden">Invisible</button>
        <button id="zero" style="width: 0; height: 0; padding: 0; border: 0; overflow: hidden">Zero</button>
        <details><summary>More</summary><button id="folded">Folded</button></details>
        <div id="host"></div>
        """;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        await WebEngine.EnsureChromiumAsync(headed: false, CancellationToken.None);
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        // The engines are registered before any context exists, as the web engine registers them.
        foreach (var (name, source) in E2E.Engine.SelectorEngines.All)
        {
            await _playwright.Selectors.RegisterAsync(name, new SelectorsRegisterOptions { Script = "(" + source + ")()" });
        }

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _page = await _browser.NewPageAsync();
        await _page.AddInitScriptAsync(PageScript.RecordClosedShadowRoots);
        await _page.GotoAsync("about:blank");
        await _page.SetContentAsync(Body);
        await _page.EvaluateAsync("() => { document.getElementById('host').attachShadow({ mode: 'closed' }).innerHTML = '<button id=\"inner\">Inner</button><label>Display name <input id=\"inner-field\"></label>'; }");
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }

    private static Task<string[]> IdsAsync(ILocator locator) => locator.EvaluateAllAsync<string[]>("els => els.map((el) => el.id)");

    [Fact]
    public async Task E2e_roots_lets_Playwrights_own_getters_match_inside_a_closed_root_and_lists_the_light_DOM_first()
    {
        // Playwright's own selectors stop at a closed root; the engine lists it as a root of its own.
        Assert.Equal(0, await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Inner" }).CountAsync());
        var roots = _page.Locator(PageScript.SearchRootsSelectorEngine + "=");
        Assert.Equal(["inner"], await IdsAsync(roots.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Inner" })));
        // The roots keep their order: the page's own matches, then the closed root's.
        Assert.Equal(["shown", "zero", "inner"], (await IdsAsync(roots.GetByRole(AriaRole.Button))).Where(id => id is "shown" or "zero" or "inner"));
        // A CSS selector is matched by the engine itself, in the query root and in every closed root under it.
        var buttons = await IdsAsync(_page.Locator(PageScript.SearchRootsSelectorEngine + "=button"));
        Assert.Contains("inner", buttons);
        Assert.Contains("gone", buttons);
        Assert.Equal(buttons.Length, buttons.Distinct().Count());
    }

    [Fact]
    public async Task E2e_label_keeps_the_controls_an_exact_label_names_and_leaves_a_label_that_merely_contains_it()
    {
        var selector = LabelSelector.ExactLabelSelector(new ExactLabelSelectorBody("Display name", "data-testid", E2E.Engine.ReadNode.SecureFieldSelector));
        // The required-field marker is aria-hidden, so it is no part of the label; "Last Name" is not "Name"; a closed root is searched too.
        Assert.Equal(["starred", "aria", "inner-field"], await IdsAsync(_page.Locator(selector)));
        var name = LabelSelector.ExactLabelSelector(new ExactLabelSelectorBody("Name", "data-testid", E2E.Engine.ReadNode.SecureFieldSelector));
        Assert.Empty(await IdsAsync(_page.Locator(name)));
        // The body is JSON, so a quote or a chain separator in a label travels whole.
        var odd = LabelSelector.ExactLabelSelector(new ExactLabelSelectorBody("a \"b\" >> c", "data-testid", E2E.Engine.ReadNode.SecureFieldSelector));
        Assert.Empty(await IdsAsync(_page.Locator(odd)));
    }

    [Fact]
    public async Task E2e_read_records_each_match_under_its_token_and_the_take_function_hands_it_back_once()
    {
        var first = ReadSelector.Create("data-testid", E2E.Engine.ReadNode.SecureFieldSelector);
        var second = ReadSelector.Create("data-testid", E2E.Engine.ReadNode.SecureFieldSelector);
        var buttons = _page.Locator("button").Locator(first.Selector).Locator(second.Selector);
        // The engine returns its query root as it is: the locator still matches every button.
        Assert.Equal(7, await buttons.CountAsync());

        var reads = await _page.Locator("button").Locator(first.Selector).EvaluateAllAsync<System.Text.Json.JsonElement>(ReadSelector.TakeReadsFunction, new { token = first.Token });
        Assert.Equal(7, reads.GetArrayLength());
        var shown = reads.EnumerateArray().Single(read => read.GetProperty("name").GetString() == "Shown");
        Assert.False(shown.GetProperty("hidden").GetBoolean());
        var gone = reads.EnumerateArray().Single(read => read.GetProperty("text").GetString() == "Gone");
        Assert.True(gone.GetProperty("hidden").GetBoolean());

        // Each read is taken back once, and another token's reads are a locate of their own.
        var again = await _page.Locator("button").EvaluateAllAsync<System.Text.Json.JsonElement>(ReadSelector.TakeReadsFunction, new { token = first.Token });
        Assert.All(again.EnumerateArray(), read => Assert.Equal(System.Text.Json.JsonValueKind.Null, read.ValueKind));
        var other = await _page.Locator("button").EvaluateAllAsync<System.Text.Json.JsonElement>(ReadSelector.TakeReadsFunction, new { token = second.Token });
        Assert.All(other.EnumerateArray(), read => Assert.Equal(System.Text.Json.JsonValueKind.Object, read.ValueKind));
    }

    [Fact]
    public async Task E2e_shown_keeps_a_match_only_when_it_reads_as_shown()
    {
        var shown = await IdsAsync(_page.Locator("button").Locator(ShownSelector.Selector));
        // Hidden by itself, by an ancestor that renders nothing, by content-visibility, by a closed details, or by a box with no size.
        Assert.Equal(["shown"], shown);
        Assert.Equal(["shown", "inner"], await IdsAsync(_page.Locator(PageScript.SearchRootsSelectorEngine + "=button").Locator(ShownSelector.Selector)));
    }
}
