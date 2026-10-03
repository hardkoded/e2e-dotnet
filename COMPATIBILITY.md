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
- Screen queries: role, text, label, test id, placeholder, display value, `filter` (`hasText` and `has`), `first`, `last`, `nth`. A locator offers every query kind, scoped to its matches' descendants. Every query takes a `TextMatch`: a string or a .NET `Regex`, tested against the whitespace-normalized text. `RoleOptions` carries `name`, `checked`, `disabled`, `selected`, `expanded`, `pressed`, and `level`, and `img` is read as `image`. A role query never matches a hidden node; the other kinds keep hidden nodes unless `Visible` is set, as upstream. `getByLabel` matches any node whose accessible name matches, as the upstream reference resolver does. Options are C# objects (`RoleOptions`, `TextMatchOptions`) instead of object literals, and `filter({ hasText, has })` is the two overloads `Filter(TextMatch)` and `Filter(Locator)`. Regular expressions use .NET syntax, not ECMAScript
- Actions: tap, click (an alias of tap), doubleTap, fill (a string or a `Secret`), pressSequentially, press, check, uncheck, clear, selectOption, focus, scrollIntoView. They wait up to the action timeout for exactly one enabled, visible match. `selectOption` takes a string that matches an option value or label. `ActionOptions.Timeout` overrides the wait per action (`PressSequentiallyOptions` adds `Delay`). The web engine's own Playwright wait still uses the configured action timeout
- Locator reads: `textContent`, `inputValue`, `getAttribute`, `isVisible`, `isHidden`, `isEnabled`, `isDisabled`, `isChecked`, `boundingBox`, `count`, `all`, `allTextContents`. They read the current screen once and do not verify an earlier `act`. Single-node reads fail with `NOT_FOUND` when nothing matches instead of waiting
- `locator.waitFor` with `attached`, `detached`, `visible` (default), and `hidden`, as `WaitForAsync(new LocatorWaitForOptions { State, Timeout })`. The timeout defaults to the action timeout and a timeout fails with `TIMEOUT`. `LocatorWaitForOptions` is named apart from `agent.waitFor`'s `WaitForOptions`
- Locator expectations: visible, hidden, text, count, enabled, disabled, checked, value. They poll until the assertion timeout
- `agent.act`, `agent.assert`, `agent.waitFor`, `agent.extract`
- Act tools `observe`, `scroll`, `scroll_to`, and `back`. They are offered when the engine declares `EngineCapabilities.Scroll` or `EngineCapabilities.History`. `scroll_to` with a target scrolls it into view. With text, it pages the viewport, or the target list, until a node reading the text is listed, then scrolls it into view. It stops when the screen stops moving
- Replay cache for a verified `act`: role, name, test id, and path. Modes are `self-finalized`, `agent-concluded`, and `missed`
- `Values.Unique` and `Secret`. Secret values are redacted from prompts, including the `assert`, `waitFor`, and `extract` statement. Redaction matches any case, JSON escapes, HTML character references, percent encoding, and collapsed inner whitespace. A known marker is never rewritten. A failed secret fill in `WebEngine` reports the Playwright message with the value and its fragments of 8 or more characters redacted. Not ported: decoding base64 runs, and values cut short at a length limit
- `app.open`, `app.back`, `app.restart`, `app.clearState`, and `app.baseUrl` (`App.BaseUrl`), and the context `platform`
- The `browser` fixture subset: `reload`, `back`, `forward`, `url`, `title`, `waitForURL`, `evaluate`, `cookies`, `setCookies`, `setViewport`, `keyboard.press`, `keyboard.type`, and `mouse.move`, `wheel`, `down`, `up`
- OpenAI-compatible tool calling

Replay runs `back` and a viewport scroll as recorded, re-finds a scrolled list before each repeat, and pages again for a `scroll_to` text. Consecutive identical scrolls are recorded as one action with a repeat count. `observe` is not recorded. Upstream scrolls the viewport when a list that filled the screen cannot be re-found. This port has no node geometry, so a lost list stops the replay.

A passing locator expectation or `agent.assert` after `act` writes the recording. `agent.assert`, `waitFor`, and `extract` always run live. Retries do not replay.

## Not ported

- The `e2e` command-line tool, the custom runner, `[E2ETest]` discovery, `--grep`, `test.only`, and the JSON report
- `@e2e-dev/mobile`, `@e2e-dev/github`, `@e2e-dev/kernel`, `@e2e-dev/eas`
- MCP server, `e2e init`, `e2e login`, OAuth subscriptions (ChatGPT, Copilot, Grok)
- Vision, screenshots, traces, and video
- Parallel workers, serial suites, project tools, and custom executors
- The full locator action set (`dragTo`, `swipe`, `hover`, `setInputFiles`, `secondaryTap`, `longPress`, pointer points). `secondaryTap` and `longPress` have no counterpart in the document engine
- Per-action options other than `timeout` (and `delay` for `pressSequentially`): click `modifiers`, tap `position`, and the `{ label, value, index }` form of `selectOption`
- Route patterns that ignore a record id. This port compares the URL path exactly and ignores the query and fragment
- Diff-only observations. Each model turn receives a full text snapshot
- Telemetry
- Browser fixture members `goto`, `locator`, `frameLocator`, `route`, `unroute`, `waitForResponse`, `onDialog`, `waitForDownload`, and the `expect(browser)` matchers (`toHaveURL`, `toHaveTitle`, `toHaveClass`)
- The upstream reporter, GitHub pull request comment, and trace viewer

## Web engine

`WebEngine` launches Chromium through Playwright and builds a semantic tree in the page: explicit roles, the upstream implicit role table (landmarks, lists, tables with rows and cells, dialogs, options, `img alt=""` as presentation, `select multiple` as listbox, and the rest), accessible name, text, test id, heading level, and the disabled, checked, expanded, selected, pressed, focused, and hidden states. As upstream, the name reads `aria-labelledby` before `aria-label` and associated labels. Disabled covers `:disabled` (including a disabled fieldset), `aria-disabled="true"` on the element, and `aria-disabled` inherited from an ancestor for the roles it applies to. Checked is the native state for checkbox and radio inputs and `aria-checked="true"` elsewhere; `mixed` reads as not checked. A node under a hidden ancestor is hidden, `role="img"` is reported as `image`, and an open `details` is expanded. A closed select lists up to 60 options under it. Each observed element keeps a stable ref for the life of the document, and actions run on the element that ref names. Firefox and WebKit launch options are not exposed yet; the package reference can drive them later.

The walk goes through open shadow roots and closed ones that page script attached (an init script records them, as upstream does). Each iframe is a boundary node, and the engine reads that frame's document through Playwright and puts it under the node, so same-origin and cross-origin frames are both listed and acted on. One observation lists at most 3000 nodes across all frames and reports `Truncated` when it stops there. `OpenAsync` waits for the `load` event.

`WebEngineOptions` carries the upstream `web()` options: `Viewport` (default 1280 by 720, `null` for no emulation), `TestIdAttribute`, `Headers`, `BasicAuth`, `UserAgent`, and `Connect` (a CDP endpoint resolver). Differences:

- `Headers` ride requests to the base URL's host. Upstream scopes them to the app's site, which can include sibling subdomains
- `Connect` has no `reconnectEndpoint`, so there is no persistent remote context, and no provider (`browser: BrowserProvider`) or `screencast` option
- `BasicAuth` takes a `Secret` password, but the value is not added to the session's redaction list

The tree is a subset of upstream's: names follow the port's simpler accname rules, nodes are listed by role or test id only (not by name, direct text, or as an empty painted `box`), and hrefs and attributes are not projected.

Password fields are marked secure and their values are omitted from the snapshot.

Playwright failures never escape as `PlaywrightException`. `WebEngine` throws `EngineException` with an upstream engine code from `EngineErrorCodes`: `NOT_ACTIONABLE` when an action timed out before its input was dispatched or the target does not take that input, `ACTION_MAY_HAVE_COMMITTED` when it timed out after dispatch, `NODE_STALE` when the element or its document is gone, `OPERATION_TIMEOUT` for a navigation or key press timeout, and `ENGINE_FAILURE` otherwise. `EngineException.Retryable` is true only for `NODE_STALE` and `FRAME_NOT_FOUND`; any other retryable claim becomes `ENGINE_FAILURE`, as upstream. A sensitive fill's value is redacted from the message. The document engine still uses `NOT_FOUND` for a ref missing from its last observation.

When a document has more nodes than the observation budget, nodes that intersect the viewport come first, the rest of the budget goes to the others in document order, and the snapshot tells the model to scroll. A scroll moves three quarters of the viewport or the scrolled element with `scrollBy`. Upstream sends a wheel gesture. `back` is the browser history.

## App and browser fixtures

`App.RestartAsync` closes the tab and opens a blank one in the same browser context, so cookies and storage survive. `App.ClearStateAsync` replaces the context with a clean one and opens a blank page. Both keep a viewport set with `Browser.SetViewportAsync`. Call `App.OpenAsync` afterwards.

Differences from upstream:

- `app.open`, `back`, `forward`, and `reload` wait for `load` within the action timeout. Upstream waits within the test timeout.
- `Browser.WaitForURLAsync` takes a string, resolved against the base URL and compared exactly, or a `Regex`. It polls until the assertion timeout or the given timeout.
- `Browser.EvaluateAsync<T>` takes the script as a string (an expression, or a function source that is called with the optional argument) and deserializes the JSON result to `T`. A throwing script fails with `EVALUATE_FAILED`.
- Fixture calls are not recorded as harness steps.

`DocumentEngine` supports `app.back` (it rebuilds the previous route), `app.restart`, and `app.clearState` (both leave a blank page and no history). It has no viewport: `DocumentPage.OnScroll` and `DocumentElement.OnScroll` let a page load more rows when it is scrolled. It has no browser, so every `Browser` member fails with `UNSUPPORTED_CAPABILITY`. A custom engine opts in by implementing `IBrowserSession`, and by overriding the default `BackAsync`, `RestartAsync`, and `ClearStateAsync` on `IEngineSession`, which otherwise fail with `UNSUPPORTED_CAPABILITY`.
