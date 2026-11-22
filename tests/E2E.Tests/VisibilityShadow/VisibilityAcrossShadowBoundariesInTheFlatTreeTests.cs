// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.VisibilityShadow;

/// <summary>
/// The reader's <c>hidden</c> state across shadow boundaries, which <c>isVisible()</c>, <c>toBeVisible()</c>, and <c>toBeHidden()</c> read:
/// content a closed <c>&lt;details&gt;</c> folds away and content under <c>content-visibility: hidden</c> are hidden in the flat tree, so a
/// shadow tree under a closed <c>&lt;details&gt;</c> and an element slotted into one, or into a closed root, read hidden the way Playwright
/// reads them. The agent's tree leaves the same buttons out. Upstream's <c>describe('visibility across shadow boundaries, in the flat tree')</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class VisibilityAcrossShadowBoundariesInTheFlatTreeTests(ChromiumPage chromium)
    : IClassFixture<ChromiumPage>,
        IAsyncLifetime
{
    private readonly List<IPage> _pages = [];

    private const string Html = """
          <details><summary>Light</summary><button id="light-in-closed">Light</button></details>
          <details><summary>Shadow</summary><x-host></x-host></details>
          <x-acc><button id="slotted">Slotted</button></x-acc>
          <x-acc-closed><button id="slotted-closed">Slotted closed</button></x-acc-closed>
          <x-sum><button id="in-summary-slot" slot="s">In summary slot</button><button id="in-body-slot">In body slot</button></x-sum>
          <details><summary><x-summary-host></x-summary-host></summary>body</details>
          <details open><summary>Open</summary><x-open-host></x-open-host></details>
          <x-cv-closed><button id="cv-closed">Closed slotted</button></x-cv-closed>
          <x-cv-open><button id="cv-open">Open slotted</button></x-cv-open>
          <x-sum-closed><button id="closed-summary-slot" slot="s">Closed summary slot</button><button id="closed-body-slot">Closed body slot</button></x-sum-closed>
          <x-manual><button id="manual-body">Manual body</button><button id="manual-summary">Manual summary</button></x-manual>
          <details><summary>First</summary><summary><button id="second-summary">Second summary</button></summary></details>
          <details><div><summary><button id="nested-summary">Nested summary</button></summary></div><summary><button id="direct-summary">Direct summary</button></summary></details>
          <x-svg-slot><button id="svg-named" slot="s">Past an SVG slot</button></x-svg-slot>
          <x-dup-slot><button id="dup-named" slot="d">First of two slots</button></x-dup-slot>
          <x-svg-first><button id="svg-first">Past an SVG slot, folded</button></x-svg-first>
          <x-nul-slot><button id="nul-named">Named with a NUL</button></x-nul-slot>
          <details id="foreign-summary-details"><button id="foreign-summary-body">Foreign summary body</button></details>
          <div id="foreign-host"></div>
          <style>.expanded::details-content { content-visibility: visible; }</style>
          <details class="expanded"><summary>CSS accordion</summary><button id="expanded-light">Expanded light</button><x-expanded-host></x-expanded-host></details>
          <x-cv-contents-closed><button id="cv-contents-closed">Contents closed</button></x-cv-contents-closed>
          <x-cv-contents-open><button id="cv-contents-open">Contents open</button></x-cv-contents-open>
          <x-cv-inline-closed><button id="cv-inline-closed">Inline closed</button></x-cv-inline-closed>
          <x-cv-contents-direct></x-cv-contents-direct>
          <x-acc-shown><button id="slotted-shown">Slotted shown</button></x-acc-shown>
          <x-acc-shown-closed><button id="slotted-shown-closed">Slotted shown closed</button></x-acc-shown-closed>
          <table><tr><td style="content-visibility:hidden"><button id="cv-td">Cell button</button></td></tr></table>
          <table><tr style="content-visibility:hidden"><td><button id="cv-tr">Row button</button></td></tr></table>
          <div style="display:contents;content-visibility:hidden">Loose text under a box-less wrapper</div>
          <style>.laid-out::details-content { display: contents; }</style>
          <details class="laid-out"><summary>Laid out</summary><button id="laid-out-light">Laid out light</button><x-laid-out-host></x-laid-out-host></details>
          <table style="content-visibility:hidden"><tr><td><button id="cv-table">Table button</button></td></tr></table>
          <x-cv-table-closed><button id="cv-table-closed">Table slotted</button></x-cv-table-closed>
          <div style="display:inline-table;content-visibility:hidden"><button id="cv-inline-table">Inline table button</button></div>
          <ruby style="content-visibility:hidden"><button id="cv-ruby">Ruby button</button></ruby>
          <ul><li style="display:inline list-item;content-visibility:hidden"><button id="cv-inline-li">Inline item button</button></li></ul>
          <svg style="content-visibility:hidden" width="100" height="40"><a href="#" id="cv-svg-link"><text x="5" y="20">Svg link</text></a></svg>
          <span style="display:inline-block;content-visibility:hidden;width:120px;height:30px"><button id="cv-inline-block">Inline block button</button></span>
          <button aria-label="Inline button" style="display:inline;content-visibility:hidden"><span id="cv-button-span">Inside an inline button</span></button>
          <button aria-label="Ruby button" style="display:ruby;content-visibility:hidden"><span id="cv-ruby-button-span">Inside a ruby button</span></button>
          <fieldset style="display:contents;content-visibility:hidden"><button id="cv-fieldset-contents">Fieldset contents</button></fieldset>
          <svg width="100" height="40"><svg style="content-visibility:hidden"><a href="#" id="cv-inner-svg-link"><text x="5" y="20">Inner svg link</text></a></svg></svg>
          <svg width="200" height="60"><foreignObject width="200" height="60"><svg style="content-visibility:hidden" width="100" height="40"><a href="#" id="cv-fo-svg-link"><text x="5" y="20">Foreign object svg link</text></a></svg></foreignObject></svg>
          <svg width="100" height="40"><svg style="display:block;content-visibility:hidden"><a href="#" id="cv-inner-block-svg-link"><text x="5" y="20">Inner block svg link</text></a></svg></svg>
          <x-ns-slot><button id="ns-slot">Namespaced slot attribute</button></x-ns-slot>
          <x-ns-name><button id="ns-name" slot="y">Namespaced slot name</button></x-ns-name>
          <fieldset id="cv-fieldset" style="display:inline;content-visibility:hidden">Loose fieldset text<button id="cv-fieldset-inline">Inline fieldset button</button></fieldset>
          <script>
            const define = (name, mode, html) => customElements.define(name, class extends HTMLElement {
              constructor() { super(); this.attachShadow({ mode }).innerHTML = html; }
            });
            define('x-host', 'open', '<button id="shadow-in-closed">Shadow</button>');
            define('x-acc', 'open', '<details><summary>Acc</summary><slot></slot></details>');
            define('x-acc-closed', 'closed', '<details><summary>Acc</summary><slot></slot></details>');
            define('x-sum', 'open', '<details><summary><slot name="s"></slot></summary><slot></slot></details>');
            define('x-summary-host', 'open', '<button id="shadow-in-summary">In summary</button>');
            define('x-open-host', 'open', '<button id="shadow-in-open">In open</button>');
            const skipping = '<div style="content-visibility:hidden;width:200px;height:40px"><slot></slot></div>';
            define('x-cv-closed', 'closed', skipping);
            define('x-cv-open', 'open', skipping);
            define('x-svg-slot', 'closed', '<details><summary>Svg</summary><svg><slot name="s"></slot></svg></details><slot name="s"></slot>');
            define('x-svg-first', 'closed', '<svg><slot></slot></svg><details><summary>Svg first</summary><slot></slot></details>');
            customElements.define('x-nul-slot', class extends HTMLElement {
              constructor() {
                super();
                const root = this.attachShadow({ mode: 'closed' });
                root.innerHTML = '<details><summary>Nul</summary><slot></slot></details><slot></slot>';
                const [folded, shown] = root.querySelectorAll('slot');
                folded.setAttribute('name', '\uFFFD');
                shown.setAttribute('name', '\u0000');
                this.querySelector('#nul-named').setAttribute('slot', '\u0000');
              }
            });
            const foreignDetails = document.querySelector('#foreign-summary-details');
            const htmlSummary = document.createElement('summary');
            htmlSummary.innerHTML = '<button id="html-summary-after-foreign">HTML summary after a foreign one</button>';
            foreignDetails.prepend(document.createElementNS('urn:e2e:not-html', 'summary'), htmlSummary);
            define('x-dup-slot', 'closed', '<details><summary>Dup</summary><slot name="d"></slot></details><slot name="d"></slot>');
            const foreign = document.createElementNS('urn:e2e:not-html', 'details');
            foreign.innerHTML = '<button id="in-foreign-details">In foreign details</button>';
            document.querySelector('#foreign-host').append(foreign);
            define('x-sum-closed', 'closed', '<details><summary><slot name="s"></slot></summary><slot></slot></details>');
            define('x-expanded-host', 'open', '<button id="expanded-shadow">Expanded shadow</button>');
            // content-visibility skips the contents of an element with a box it can contain; these have none.
            define('x-cv-contents-closed', 'closed', '<div style="content-visibility:hidden;display:contents"><slot></slot></div>');
            define('x-cv-contents-open', 'open', '<div style="content-visibility:hidden;display:contents"><slot></slot></div>');
            const shownBody = '<style>details::details-content { content-visibility: visible; }</style><details><summary>Shown</summary><slot></slot></details>';
            define('x-acc-shown', 'open', shownBody);
            define('x-acc-shown-closed', 'closed', shownBody);
            define('x-cv-contents-direct', 'open', '<div style="content-visibility:hidden;display:contents"><button id="cv-contents-direct">Contents direct</button></div>');
            define('x-cv-inline-closed', 'closed', '<span style="content-visibility:hidden"><slot></slot></span>');
            define('x-laid-out-host', 'open', '<button id="laid-out-shadow">Laid out shadow</button>');
            customElements.define('x-ns-slot', class extends HTMLElement {
              constructor() {
                super();
                this.attachShadow({ mode: 'closed' }).innerHTML = '<details><summary>Ns</summary><slot name="x"></slot></details><slot></slot>';
                this.querySelector('#ns-slot').setAttributeNS('urn:e2e:not-html', 'slot', 'x');
              }
            });
            customElements.define('x-ns-name', class extends HTMLElement {
              constructor() {
                super();
                const root = this.attachShadow({ mode: 'closed' });
                root.innerHTML = '<details><summary>Ns name</summary><slot></slot></details><slot name="y"></slot>';
                root.querySelector('slot').setAttributeNS('urn:e2e:not-html', 'name', 'y');
              }
            });
            define('x-cv-table-closed', 'closed', '<table style="content-visibility:hidden"><tr><td><slot></slot></td></tr></table>');
            customElements.define('x-manual', class extends HTMLElement {
              constructor() {
                super();
                const root = this.attachShadow({ mode: 'closed', slotAssignment: 'manual' });
                root.innerHTML = '<svg><slot></slot></svg><details><summary><slot id="label"></slot></summary><slot id="body"></slot></details>';
                root.getElementById('label').assign(this.querySelector('#manual-summary'));
                root.getElementById('body').assign(this.querySelector('#manual-body'));
              }
            });
          </script>
        """;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var page in _pages)
        {
            await page.CloseAsync();
        }
    }

    // Upstream opens the page once in beforeAll; custom elements cannot be defined twice on one, so each test opens its own.
    private async Task<IPage> SetUpAsync()
    {
        var page = await OtherPageAsync();
        await page.SetContentAsync(Html);
        return page;
    }

    // Upstream's hiddenOf: the reader's hidden state for the selector on a page, next to what Playwright says of the same element.
    private async Task<(bool Reader, bool Playwright)> HiddenOfAsync(IPage on, string selector)
    {
        var locator = on.Locator(selector);
        var read = await chromium.ReadAsync(locator);
        return (read!.Hidden, await locator.IsHiddenAsync());
    }

    // A page of its own with the closed-root record, as the engine's context opens one.
    private async Task<IPage> OtherPageAsync()
    {
        var page = await chromium.NewPageAsync();
        _pages.Add(page);
        return page;
    }

    [Theory]
    [InlineData("a light button under closed details", "#light-in-closed", true)]
    [InlineData("a shadow button under closed details", "#shadow-in-closed", true)]
    [InlineData("a button slotted into closed details in an open root", "#slotted", true)]
    [InlineData("a button slotted into closed details in a closed root", "#slotted-closed", true)]
    [InlineData("a button slotted into the summary of closed details", "#in-summary-slot", false)]
    [InlineData("a button slotted into the body of closed details", "#in-body-slot", true)]
    [InlineData("a shadow button in the summary of closed details", "#shadow-in-summary", false)]
    [InlineData("a shadow button under open details", "#shadow-in-open", false)]
    [InlineData("a button slotted under content-visibility: hidden in a closed root", "#cv-closed", true)]
    [InlineData("a button slotted under content-visibility: hidden in an open root", "#cv-open", true)]
    [InlineData(
        "a button slotted by name into the summary of closed details in a closed root",
        "#closed-summary-slot",
        false
    )]
    [InlineData("a button slotted into the body of closed details in a closed root", "#closed-body-slot", true)]
    [InlineData(
        "a button assigned by hand into the summary of closed details in a closed root",
        "#manual-summary",
        false
    )]
    [InlineData("a button assigned by hand into the body of closed details in a closed root", "#manual-body", true)]
    [InlineData("a button in a second summary of closed details", "#second-summary", true)]
    [InlineData("a button in a summary that is not a child of closed details", "#nested-summary", true)]
    [InlineData("a button in the first child summary of closed details, after a nested one", "#direct-summary", false)]
    [InlineData(
        "a button slotted into an HTML slot of a closed root, past an SVG element named slot under closed details",
        "#svg-named",
        false
    )]
    [InlineData(
        "a button slotted into the first of two slots of its name in a closed root, under closed details",
        "#dup-named",
        true
    )]
    [InlineData(
        "a button slotted into an HTML slot under closed details in a closed root, after an SVG element named slot",
        "#svg-first",
        true
    )]
    [InlineData("a button slotted by a name holding U+0000 into a shown slot of a closed root", "#nul-named", false)]
    [InlineData(
        "a button in the first HTML summary of closed details, after a summary element that is not HTML",
        "#html-summary-after-foreign",
        false
    )]
    [InlineData(
        "a button in the body of closed details whose first child is a summary element that is not HTML",
        "#foreign-summary-body",
        true
    )]
    [InlineData("a light button in closed details whose body CSS shows", "#expanded-light", false)]
    [InlineData("a shadow button in closed details whose body CSS shows", "#expanded-shadow", false)]
    [InlineData(
        "a button slotted under content-visibility: hidden on display: contents in a closed root",
        "#cv-contents-closed",
        false
    )]
    [InlineData(
        "a button slotted under content-visibility: hidden on display: contents in an open root",
        "#cv-contents-open",
        false
    )]
    [InlineData(
        "a button slotted under content-visibility: hidden on an inline element in a closed root",
        "#cv-inline-closed",
        false
    )]
    [InlineData("a shadow button under content-visibility: hidden on display: contents", "#cv-contents-direct", false)]
    [InlineData("a button in a table row with content-visibility: hidden", "#cv-tr", false)]
    [InlineData("a button slotted into closed details whose body CSS shows, in an open root", "#slotted-shown", false)]
    [InlineData(
        "a button slotted into closed details whose body CSS shows, in a closed root",
        "#slotted-shown-closed",
        false
    )]
    [InlineData("a button under a details element that is not HTML", "#in-foreign-details", false)]
    [InlineData(
        "a light button in closed details whose body is laid out as display: contents",
        "#laid-out-light",
        false
    )]
    [InlineData(
        "a shadow button in closed details whose body is laid out as display: contents",
        "#laid-out-shadow",
        false
    )]
    [InlineData("a button in a table with content-visibility: hidden", "#cv-table", false)]
    [InlineData(
        "a button slotted under a table with content-visibility: hidden in a closed root",
        "#cv-table-closed",
        false
    )]
    [InlineData("a button under an inline table with content-visibility: hidden", "#cv-inline-table", false)]
    [InlineData("a button under ruby with content-visibility: hidden", "#cv-ruby", false)]
    [InlineData("a button under an inline list item with content-visibility: hidden", "#cv-inline-li", false)]
    [InlineData("a link in an inline svg with content-visibility: hidden", "#cv-svg-link", true)]
    [InlineData("a button under an inline block with content-visibility: hidden", "#cv-inline-block", true)]
    [InlineData("text in an inline button with content-visibility: hidden", "#cv-button-span", true)]
    [InlineData("text in a ruby button with content-visibility: hidden", "#cv-ruby-button-span", true)]
    [InlineData(
        "a link in an svg in a foreignObject, with content-visibility: hidden on that svg",
        "#cv-fo-svg-link",
        true
    )]
    [InlineData(
        "a button whose slot attribute is in a namespace, so it goes to the default slot of a closed root",
        "#ns-slot",
        false
    )]
    [InlineData(
        "a button slotted by name past a slot whose name attribute is in a namespace, in a closed root",
        "#ns-name",
        false
    )]
    [InlineData(
        "a button in a display: contents fieldset with content-visibility: hidden",
        "#cv-fieldset-contents",
        false
    )]
    public async Task Case(string description, string selector, bool hidden)
    {
        _ = description;
        var page = await SetUpAsync();
        Assert.Equal((hidden, hidden), await HiddenOfAsync(page, selector));
    }

    // Partial: Microsoft.Playwright 1.64 bundles a Chromium that reads content under content-visibility: hidden on a nested svg as hidden, so
    // its isHidden disagrees with upstream's playwright-core 1.65.1. Only the reader's half is checked until the package moves (see the PR report).
    [Theory]
    [InlineData(
        "a link in an svg nested in another, with content-visibility: hidden on the inner one",
        "#cv-inner-svg-link",
        false
    )]
    [InlineData(
        "a link in a block svg nested in another, with content-visibility: hidden on the inner one",
        "#cv-inner-block-svg-link",
        false
    )]
    public async Task Partial_Case(string description, string selector, bool hidden)
    {
        _ = description;
        var page = await SetUpAsync();
        var read = await chromium.ReadAsync(page.Locator(selector));
        Assert.Equal(hidden, read!.Hidden);
    }

    [Fact]
    public async Task Lists_only_the_buttons_that_render_in_the_agent_tree()
    {
        var page = await SetUpAsync();
        var nodes = await chromium.CaptureAsync(200, page);
        var buttons = nodes.Where(node => node.Role == "button").Select(node => node.Name);
        Assert.Equal(
            [
                "In summary slot",
                "In summary",
                "In open",
                "Closed summary slot",
                "Manual summary",
                "Direct summary",
                "Past an SVG slot",
                "Named with a NUL",
                "HTML summary after a foreign one",
                "In foreign details",
                "Expanded light",
                "Expanded shadow",
                "Contents closed",
                "Contents open",
                "Inline closed",
                "Contents direct",
                "Slotted shown",
                "Slotted shown closed",
                "Row button",
                "Laid out light",
                "Laid out shadow",
                "Table button",
                "Table slotted",
                "Inline table button",
                "Ruby button",
                "Inline item button",
                "Inline button",
                "Ruby button",
                "Fieldset contents",
                "Namespaced slot attribute",
                "Namespaced slot name",
            ],
            buttons
        );
        Assert.Contains(nodes, node =>
            new[] { node.Name, node.Text }.Any(text => text?.Contains("Loose text under a box-less wrapper") == true)
        );
    }

    [Fact]
    public async Task Hides_a_button_under_a_closed_details_adopted_from_another_frame()
    {
        var other = await OtherPageAsync();
        await other.SetContentAsync(
            "<iframe srcdoc=\"<details><summary>Adopted</summary><button id=adopted>Adopted</button></details>\"></iframe>"
        );
        await other.WaitForFunctionAsync(
            "() => document.querySelector('iframe')?.contentDocument?.querySelector('details') != null"
        );
        await other.EvaluateAsync(
            "() => document.body.append(document.querySelector('iframe').contentDocument.querySelector('details'))"
        );
        Assert.Equal((true, true), await HiddenOfAsync(other, "#adopted"));
        await other.EvaluateAsync("() => document.querySelector('details').setAttribute('open', '')");
        Assert.Equal((false, false), await HiddenOfAsync(other, "#adopted"));
    }

    [Fact]
    public async Task Reads_the_slots_of_a_closed_root_once_per_read_however_many_children_its_host_slots()
    {
        var other = await OtherPageAsync();
        var rows =
            string.Concat(Enumerable.Range(0, 1000).Select(index => $"<span slot=\"s{index}\">Row</span>"))
            + string.Concat(Enumerable.Repeat("<span>Row</span>", 1000));
        var slots =
            string.Concat(Enumerable.Range(0, 1000).Select(index => $"<slot name=\"s{index}\"></slot>"))
            + "<slot></slot>";
        await other.SetContentAsync(ManyHost(rows, slots));
        await other.EvaluateAsync(
            """
            () => {
              const counts = { scans: 0, lists: 0 };
              globalThis.counts = counts;
              const scan = HTMLSlotElement.prototype.assignedElements;
              HTMLSlotElement.prototype.assignedElements = function (options) {
                counts.scans += 1;
                return scan.call(this, options);
              };
              const list = ShadowRoot.prototype.querySelectorAll;
              ShadowRoot.prototype.querySelectorAll = function (selectors) {
                if (selectors === "slot") counts.lists += 1;
                return list.call(this, selectors);
              };
            }
            """
        );
        await chromium.CaptureAsync(200, other);
        // One listing of the root's 1,001 slots, not one per slotted child, and a root that assigns by name never lists what a slot holds.
        var counts = await other.EvaluateAsync<int[]>("() => [globalThis.counts.scans, globalThis.counts.lists]");
        Assert.Equal(new[] { 0, 1 }, counts);
    }

    [Theory]
    [InlineData("a named slot each", 100)]
    [InlineData("one default slot", 1000)]
    public async Task Reads_one_match_of_a_locator_slotted_into_a_closed_root_by_name_without_listing_what_the_slots_hold(
        string shape,
        int count
    )
    {
        var other = await OtherPageAsync();
        var named = shape == "a named slot each";
        var slots =
            (named ? string.Concat(Enumerable.Range(0, count).Select(index => $"<slot name=\"s{index}\"></slot>")) : "")
            + "<slot></slot>";
        var rows = string.Concat(
            Enumerable
                .Range(0, count)
                .Select(index => named ? $"<button slot=\"s{index}\">Row</button>" : "<button>Row</button>")
        );
        await other.SetContentAsync(ManyHost(rows, slots));
        var work = await other.EvaluateAsync<int[]>(
            """
            (engineSource) => {
              const counts = { held: 0, lists: 0, shown: 0 };
              const scan = HTMLSlotElement.prototype.assignedElements;
              HTMLSlotElement.prototype.assignedElements = function (options) {
                const found = scan.call(this, options);
                counts.held += found.length;
                return found;
              };
              const list = ShadowRoot.prototype.querySelectorAll;
              ShadowRoot.prototype.querySelectorAll = function (selectors) {
                if (selectors === "slot") counts.lists += 1;
                return list.call(this, selectors);
              };
              const engine = (new Function("return (" + engineSource + ")()"))();
              // The visible: true engine, called once per candidate the way a locator reads its matches.
              for (const button of Array.from(document.querySelectorAll("many-host > button"))) counts.shown += engine.queryAll(button, "").length;
              return [counts.held, counts.lists, counts.shown];
            }
            """,
            ShownSelector.ShownSelectorEngineSource
        );
        // Every match shown, one listing of the slots per match, and nothing a slot holds read: the slot comes from the button's name.
        Assert.Equal(new[] { 0, count, count }, work);
    }

    [Fact]
    public async Task Looks_for_the_summary_of_a_wide_closed_details_once_per_read()
    {
        var other = await OtherPageAsync();
        // No summary, so every look walks all 2,000 children.
        await other.SetContentAsync($"<details>{string.Concat(Enumerable.Repeat("<span>Row</span>", 2000))}</details>");
        await other.EvaluateAsync(CountDetailsReads("['children', 'firstElementChild', 'nextElementSibling']"));
        await chromium.CaptureAsync(3000, other);
        // One walk of the children, plus whatever else of the read steps through them; never one walk per child.
        Assert.True(await other.EvaluateAsync<int>("() => globalThis.counter.reads") <= 3 * 2000);
    }

    [Fact]
    public async Task Never_looks_for_the_summary_of_an_open_details_so_reading_its_children_one_call_each_stays_linear()
    {
        var other = await OtherPageAsync();
        // No summary, so a look would walk all 100 children, once per reader call.
        await other.SetContentAsync(
            $"<details open>{string.Concat(Enumerable.Repeat("<button>Row</button>", 100))}</details>"
        );
        await other.EvaluateAsync(CountDetailsReads("['firstElementChild', 'nextElementSibling']"));
        // A locator reads each match in its own reader call.
        for (var index = 0; index < 100; index++)
        {
            await chromium.ReadAsync(other.Locator("details > button").Nth(index));
        }

        // A look per call would be 100 x 100 steps; each call's own work on its button is a few.
        Assert.True(await other.EvaluateAsync<int>("() => globalThis.counter.reads") <= 5 * 100);
    }

    [Fact]
    public async Task Reads_a_table_cell_with_content_visibility_hidden_as_skipping_what_is_in_it()
    {
        var page = await SetUpAsync();
        // Chromium skips a table cell's contents, as checkVisibility() reports; Firefox and WebKit paint them.
        var read = await chromium.ReadAsync(page.Locator("#cv-td"));
        Assert.True(read!.Hidden);
    }

    [Fact]
    public async Task Reads_an_inline_fieldset_with_content_visibility_hidden_as_skipping_what_is_in_it()
    {
        var page = await SetUpAsync();
        // A fieldset makes a box of its own whatever its display; Chromium and Firefox skip its contents, WebKit paints them.
        Assert.Equal((true, true), await HiddenOfAsync(page, "#cv-fieldset-inline"));
        // Its own text renders no more than its button does, so the agent's tree does not carry it.
        Assert.Equal("", await page.Locator("#cv-fieldset").EvaluateAsync<string>("el => el.innerText"));
        var nodes = await chromium.CaptureAsync(200, page);
        Assert.DoesNotContain(nodes, node =>
            new[] { node.Name, node.Text }.Any(text => text?.Contains("Loose fieldset text") == true)
        );
    }

    [Fact]
    public async Task Does_not_walk_into_an_inline_fieldset_with_content_visibility_hidden_while_building_the_agent_tree()
    {
        var other = await OtherPageAsync();
        await other.SetContentAsync(
            $"<button>Before</button><fieldset id=\"skipped\" style=\"display:inline;content-visibility:hidden\">{string.Concat(Enumerable.Repeat("<button>Row</button>", 200))}</fieldset>"
        );
        await other.EvaluateAsync(
            """
            () => {
              const counter = { styled: 0 };
              globalThis.counter = counter;
              const skipped = document.getElementById("skipped");
              const style = window.getComputedStyle;
              window.getComputedStyle = function (el, pseudo) {
                if (el !== skipped && skipped.contains(el)) counter.styled += 1;
                return style.call(window, el, pseudo);
              };
            }
            """
        );
        await chromium.CaptureAsync(500, other);
        // Nothing under the fieldset renders, so the walk stops at it rather than reading each of its 200 buttons.
        Assert.Equal(0, await other.EvaluateAsync<int>("() => globalThis.counter.styled"));
    }

    private static string ManyHost(string rows, string slots) =>
        $$"""
            <many-host>{{rows}}</many-host><script>
              customElements.define('many-host', class extends HTMLElement {
                constructor() { super(); this.attachShadow({ mode: 'closed' }).innerHTML = '<div>{{slots}}</div>'; }
              });
            </script>
            """;

    private static string CountDetailsReads(string properties) =>
        $$"""
            () => {
              const counter = { reads: 0 };
              globalThis.counter = counter;
              for (const name of {{properties}}) {
                const property = Object.getOwnPropertyDescriptor(Element.prototype, name) ?? Object.getOwnPropertyDescriptor(Document.prototype, name);
                Object.defineProperty(Element.prototype, name, {
                  configurable: true,
                  get() {
                    if (this.localName === 'details' || this.parentElement?.localName === 'details') counter.reads += 1;
                    return property.get.call(this);
                  },
                });
              }
            }
            """;
}
