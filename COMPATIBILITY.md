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
- `app.open`, `app.back`, `app.restart`, `app.clearState`, and `app.baseUrl` (`App.BaseUrl`), and the context `platform`
- The `browser` fixture subset: `reload`, `back`, `forward`, `url`, `title`, `waitForURL`, `evaluate`, `cookies`, `setCookies`, `setViewport`, `keyboard.press`, `keyboard.type`, and `mouse.move`, `wheel`, `down`, `up`
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
- Browser fixture members `goto`, `locator`, `frameLocator`, `route`, `unroute`, `waitForResponse`, `onDialog`, `waitForDownload`, and the `expect(browser)` matchers (`toHaveURL`, `toHaveTitle`, `toHaveClass`)
- The upstream reporter, GitHub pull request comment, and trace viewer

## Web engine

`WebEngine` launches Chromium through Playwright and builds a semantic tree in the page: explicit roles, common implicit roles, accessible name, text, `data-testid`, disabled, checked, and hidden. Each observed element keeps a stable ref for the life of the document, and actions run on the element that ref names. Firefox and WebKit launch options are not exposed yet; the package reference can drive them later.

Password fields are marked secure and their values are omitted from the snapshot.

## App and browser fixtures

`App.RestartAsync` closes the tab and opens a blank one in the same browser context, so cookies and storage survive. `App.ClearStateAsync` replaces the context with a clean one and opens a blank page. Both keep a viewport set with `Browser.SetViewportAsync`. Call `App.OpenAsync` afterwards.

Differences from upstream:

- `app.open`, `back`, `forward`, and `reload` wait for `DOMContentLoaded` within the action timeout. Upstream waits for `load` within the test timeout.
- `Browser.WaitForURLAsync` takes a string, resolved against the base URL and compared exactly, or a `Regex`. It polls until the assertion timeout or the given timeout.
- `Browser.EvaluateAsync<T>` takes the script as a string (an expression, or a function source that is called with the optional argument) and deserializes the JSON result to `T`. A throwing script fails with `EVALUATE_FAILED`.
- Fixture calls are not recorded as harness steps.

`DocumentEngine` supports `app.back` (it rebuilds the previous route), `app.restart`, and `app.clearState` (both leave a blank page and no history). It has no browser, so every `Browser` member fails with `UNSUPPORTED_CAPABILITY`. A custom engine opts in by implementing `IBrowserSession`, and by overriding the default `BackAsync`, `RestartAsync`, and `ClearStateAsync` on `IEngineSession`, which otherwise fail with `UNSUPPORTED_CAPABILITY`.
