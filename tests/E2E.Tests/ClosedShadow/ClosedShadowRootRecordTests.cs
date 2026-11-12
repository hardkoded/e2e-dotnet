// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.ClosedShadow;

/// <summary>
/// Upstream's <c>describe('closed shadow root record')</c> (<c>packages/web/tests/unit/closed-shadow.test.ts</c>): the closed-root record
/// the init script installs and the <c>e2e-roots=</c> and <c>e2e-closed=</c> engines built on it. The engines run as page code against a
/// scripted document, as upstream runs them.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ClosedShadowRootRecordTests(ChromiumPage chromium) : IClassFixture<ChromiumPage>
{
    [Fact]
    public async Task Counts_the_closed_roots_the_wrapped_attachShadow_records_and_leaves_open_ones_out()
    {
        var recorded = await chromium.Page.EvaluateAsync<string>(
            """
            () => {
              const record = globalThis[Symbol.for("e2e.closedShadowRoots")];
              const openHost = document.createElement("div");
              const closedHost = document.createElement("div");
              const before = record.count;
              const open = openHost.attachShadow({ mode: "open" });
              const afterOpen = record.count;
              const closed = closedHost.attachShadow({ mode: "closed" });
              return [record instanceof WeakMap, before, open.mode, afterOpen, record.has(openHost), record.get(closedHost) === closed, record.count].join(",");
            }
            """);
        Assert.Equal("true,0,open,0,false,true,1", recorded);

        // Installed once per document: a second run leaves the record and the wrapper alone.
        var unchanged = await chromium.Page.EvaluateAsync<bool>(
            """
            (script) => {
              const key = Symbol.for("e2e.closedShadowRoots");
              const record = globalThis[key];
              const attachShadow = Element.prototype.attachShadow;
              (0, eval)(script);
              return globalThis[key] === record && Element.prototype.attachShadow === attachShadow;
            }
            """,
            PageScript.RecordClosedShadowRoots);
        Assert.True(unchanged);
    }

    [Fact]
    public async Task Answers_a_semantic_query_with_the_root_alone_without_walking_while_no_closed_root_was_attached()
    {
        // A page of its own, since the other tests attach closed roots to the shared one.
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        await page.AddInitScriptAsync(PageScript.RecordClosedShadowRoots);
        await page.GotoAsync("about:blank");
        var answered = await page.EvaluateAsync<string>(
            """
            (source) => {
              const record = globalThis[Symbol.for("e2e.closedShadowRoots")];
              const search = (0, eval)(`(${source})()`);
              const root = (nodeType, elements = []) => {
                const scripted = { nodeType, shadowRoot: null, walked: [] };
                scripted.querySelectorAll = (selector) => { scripted.walked.push(selector); return elements; };
                return scripted;
              };
              const document = root(9);
              const alone = search.queryAll(document, '');
              const first = alone.length === 1 && alone[0] === document && document.walked.length === 0;
              // The count is read at query time, so a root attached later is searched.
              const host = root(1);
              const closed = root(9);
              record.set(host, closed);
              record.count = 1;
              const walked = root(9, [host]);
              const found = search.queryAll(walked, '');
              const second = found.length === 2 && found[0] === walked && found[1] === closed && walked.walked.includes('*');
              return `${first},${second}`;
            }
            """,
            PageScript.SearchRootsSelectorEngineSource);
        Assert.Equal("true,true", answered);
    }

    [Fact]
    public async Task Lets_the_mask_engine_find_nothing_without_walking_while_no_closed_root_was_attached_and_refuses_an_untracked_document()
    {
        const string Probe =
            """
            (source) => {
              const engine = (0, eval)(`(${source})()`);
              let walks = 0;
              const document = { nodeType: 9, shadowRoot: null, querySelectorAll: () => { walks += 1; return []; } };
              try {
                const found = engine.queryAll(document, 'input[type="password" i]').length;
                return `${found},${walks}`;
              } catch (error) {
                return error.message;
              }
            }
            """;
        // A page of its own, since the other test attaches a closed root to the shared one.
        await using var trackedContext = await NewContextAsync();
        var tracked = await trackedContext.NewPageAsync();
        await tracked.AddInitScriptAsync(PageScript.RecordClosedShadowRoots);
        await tracked.GotoAsync("about:blank");
        Assert.Equal("0,0", await tracked.EvaluateAsync<string>(Probe, PageScript.ClosedShadowSelectorEngineSource));

        // A page that never ran the init script has no record, and a mask that cannot prove its coverage fails closed.
        await using var untrackedContext = await NewContextAsync();
        var untracked = await untrackedContext.NewPageAsync();
        await untracked.GotoAsync("about:blank");
        Assert.Contains("tracking is unavailable", await untracked.EvaluateAsync<string>(Probe, PageScript.ClosedShadowSelectorEngineSource), StringComparison.Ordinal);
        // The roots engine answers as Playwright alone would: the document is the one root.
        Assert.Equal(
            "1,0",
            await untracked.EvaluateAsync<string>(
                """
                (source) => {
                  const engine = (0, eval)(`(${source})()`);
                  let walks = 0;
                  const document = { nodeType: 9, shadowRoot: null, querySelectorAll: () => { walks += 1; return []; } };
                  const found = engine.queryAll(document, '');
                  return `${found.length === 1 && found[0] === document ? 1 : 0},${walks}`;
                }
                """,
                PageScript.SearchRootsSelectorEngineSource));
    }

    [Fact]
    public async Task Keeps_the_record_tracked_on_a_page_that_replaced_the_global_WeakMap()
    {
        // A page of its own, since the page replaces a global for good.
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        await page.AddInitScriptAsync(PageScript.RecordClosedShadowRoots);
        await page.GotoAsync("about:blank");
        var found = await page.EvaluateAsync<string>(
            """
            (source) => {
              const host = document.createElement("div");
              document.body.append(host);
              const root = host.attachShadow({ mode: "closed" });
              root.innerHTML = '<input type="password">';
              const field = root.querySelector("input");
              globalThis.WeakMap = Map;
              const engine = (0, eval)(`(${source})()`);
              const matches = engine.queryAll(document, 'input[type="password" i]');
              return matches.length + "," + (matches[0] === field);
            }
            """,
            PageScript.ClosedShadowSelectorEngineSource);
        Assert.Equal("1,true", found);
        Assert.True(await page.EvaluateAsync<bool>(PageScript.TracksClosedShadowRoots));
    }

    private async Task<ContextLease> NewContextAsync() => new(await chromium.Page.Context.Browser!.NewContextAsync());

    private sealed class ContextLease(IBrowserContext context) : IAsyncDisposable
    {
        public Task<IPage> NewPageAsync() => context.NewPageAsync();

        public async ValueTask DisposeAsync() => await context.CloseAsync();
    }
}
