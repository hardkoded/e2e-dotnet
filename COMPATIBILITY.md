# Compatibility

Upstream parity target: [tester-army/e2e](https://github.com/tester-army/e2e) commit `f7c075666672d128f79ef2ec347fda67ea9e42ce` (`fix(expect): waitFor attached/detached, absent-frame absence, toHaveProperty primitives, toHaveURL ignoreCase`), recorded from the default branch on 2026-10-02.

Names are C# versions of the JavaScript API: `agent.act` is `ActAsync`, `screen.getByRole` is `GetByRole`, `expect(locator).toContainText` is `Expect.That(locator).ToContainTextAsync`, and `unique()` is `Values.Unique`.

## Package map

| JavaScript | .NET |
| --- | --- |
| `e2e` and `@e2e-dev/web` | `E2E` (`WebEngine` is Playwright, in the same package) |
| NUnit `[Test]` | `E2E.NUnit.E2ETest`. The fixture commits the replay cache from the NUnit result |
| `e2e.config.ts` | `E2EConfig.Load` reads `e2e.config.json`. A host applies the values on the NUnit fixture |
| Vercel AI SDK model | `OpenAiCompatibleModel` (chat completions and tool calls) |
| — | `DocumentEngine`, an in-memory page for hosts that do not want a browser |

## Ported

- NUnit `[Test]`, `[SetUp]`, `[TearDown]`, `[OneTimeSetUp]`, and `[OneTimeTearDown]`
- `Assert.Ignore`, `[Retry]`, `[Timeout]`, and `[Category]`
- Screen queries: role, text, label, test id, placeholder, `filter`, `first`, `nth`
- Actions: tap, fill, press, check, uncheck, clear. They wait up to the action timeout for exactly one enabled match
- Locator expectations: visible, hidden, text, count, enabled, disabled, checked, value. They poll until the assertion timeout
- `agent.act`, `agent.assert`, `agent.waitFor`, `agent.extract`
- Act tools `observe`, `scroll`, `scroll_to`, and `back`. They are offered when the engine declares `EngineCapabilities.Scroll` or `EngineCapabilities.History`. `scroll_to` with a target scrolls it into view. With text, it pages the viewport, or the target list, until a node reading the text is listed, then scrolls it into view. It stops when the screen stops moving
- Replay cache for a verified `act`: role, name, test id, and path. Modes are `self-finalized`, `agent-concluded`, and `missed`
- `Values.Unique` and `Secret`. Secret values are redacted from prompts
- OpenAI-compatible tool calling

Replay runs `back` and a viewport scroll as recorded, re-finds a scrolled list before each repeat, and pages again for a `scroll_to` text. Consecutive identical scrolls are recorded as one action with a repeat count. `observe` is not recorded. Upstream scrolls the viewport when a list that filled the screen cannot be re-found. This port has no node geometry, so a lost list stops the replay.

A passing locator expectation or `agent.assert` after `act` writes the recording. `agent.assert`, `waitFor`, and `extract` always run live. Retries do not replay.

## Not ported

- The `e2e` command-line tool, the custom runner, `[E2ETest]` discovery, `--grep`, `test.only`, and the JSON report
- `@e2e-dev/mobile`, `@e2e-dev/github`, `@e2e-dev/kernel`, `@e2e-dev/eas`
- MCP server, `e2e init`, `e2e login`, OAuth subscriptions (ChatGPT, Copilot, Grok)
- Vision, screenshots, traces, and video
- Parallel workers, serial suites, project tools, and custom executors
- The full locator action set (`dragTo`, `swipe`, `hover`, `setInputFiles`, pointer points)
- Route patterns that ignore a record id. This port compares the URL path exactly and ignores the query and fragment
- Diff-only observations. Each model turn receives a full text snapshot
- Telemetry
- The upstream reporter, GitHub pull request comment, and trace viewer

## Web engine

`WebEngine` launches Chromium through Playwright and builds a semantic tree in the page: explicit roles, common implicit roles, accessible name, text, `data-testid`, disabled, checked, and hidden. Each observed element keeps a stable ref for the life of the document, and actions run on the element that ref names. Firefox and WebKit launch options are not exposed yet; the package reference can drive them later.

Password fields are marked secure and their values are omitted from the snapshot.

An observation holds at most 500 nodes. When a page has more, nodes that intersect the viewport come first, the rest of the budget goes to the others in document order, and the snapshot tells the model to scroll. A scroll moves three quarters of the viewport or the scrolled element with `scrollBy`. Upstream sends a wheel gesture. `back` is the browser history.

`DocumentEngine` has no viewport. `DocumentPage.OnScroll` and `DocumentElement.OnScroll` let a page load more rows when it is scrolled, and `back` reopens the previous route.
