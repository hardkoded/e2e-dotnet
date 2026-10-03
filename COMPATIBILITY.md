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
- Replay cache for a verified `act`: role, name, test id, and path. Modes are `self-finalized`, `agent-concluded`, and `missed`
- `Values.Unique` and `Secret`. Secret values are redacted from prompts
- OpenAI-compatible tool calling

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

`WebEngine` launches Chromium through Playwright and builds a semantic tree in the page: explicit roles, the upstream implicit role table (landmarks, lists, tables with rows and cells, dialogs, options, `img alt=""` as presentation, `select multiple` as listbox, and the rest), accessible name, text, test id, and the disabled, checked, expanded, selected, pressed, focused, and hidden states. As upstream, the name reads `aria-labelledby` before `aria-label` and associated labels. Disabled covers `:disabled` (including a disabled fieldset), `aria-disabled="true"` on the element, and `aria-disabled` inherited from an ancestor for the roles it applies to. Checked is the native state for checkbox and radio inputs and `aria-checked="true"` elsewhere; `mixed` reads as not checked. A closed select lists up to 60 options under it. Each observed element keeps a stable ref for the life of the document, and actions run on the element that ref names. Firefox and WebKit launch options are not exposed yet; the package reference can drive them later.

The walk goes through open shadow roots and closed ones that page script attached (an init script records them, as upstream does). Each iframe is a boundary node, and the engine reads that frame's document through Playwright and puts it under the node, so same-origin and cross-origin frames are both listed and acted on. One observation lists at most 3000 nodes across all frames and reports `Truncated` when it stops there. `OpenAsync` waits for the `load` event.

`WebEngineOptions` carries the upstream `web()` options: `Viewport` (default 1280 by 720, `null` for no emulation), `TestIdAttribute`, `Headers`, `BasicAuth`, `UserAgent`, and `Connect` (a CDP endpoint resolver). Differences:

- `Headers` ride requests to the base URL's host. Upstream scopes them to the app's site, which can include sibling subdomains
- `Connect` has no `reconnectEndpoint`, so there is no persistent remote context, and no provider (`browser: BrowserProvider`) or `screencast` option
- `BasicAuth` takes a `Secret` password, but the value is not added to the session's redaction list

The tree is a subset of upstream's: names follow the port's simpler accname rules, nodes are listed by role or test id only (not by name, direct text, or as an empty painted `box`), and hrefs and attributes are not projected.

Password fields are marked secure and their values are omitted from the snapshot.

Playwright failures never escape as `PlaywrightException`. `WebEngine` throws `EngineException` with an upstream engine code from `EngineErrorCodes`: `NOT_ACTIONABLE` when an action timed out before its input was dispatched or the target does not take that input, `ACTION_MAY_HAVE_COMMITTED` when it timed out after dispatch, `NODE_STALE` when the element or its document is gone, `OPERATION_TIMEOUT` for a navigation or key press timeout, and `ENGINE_FAILURE` otherwise. `EngineException.Retryable` is true only for `NODE_STALE` and `FRAME_NOT_FOUND`; any other retryable claim becomes `ENGINE_FAILURE`, as upstream. A sensitive fill's value is redacted from the message. The document engine still uses `NOT_FOUND` for a ref missing from its last observation.
