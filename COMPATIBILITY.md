# Compatibility

Upstream parity target: [tester-army/e2e](https://github.com/tester-army/e2e) commit `f7c075666672d128f79ef2ec347fda67ea9e42ce` (`fix(expect): waitFor attached/detached, absent-frame absence, toHaveProperty primitives, toHaveURL ignoreCase`), recorded from the default branch on 2026-10-02.

Names are C# versions of the JavaScript API: `agent.act` is `ActAsync`, `screen.getByRole` is `GetByRole`, `expect(locator).toContainText` is `Expect.That(locator).ToContainTextAsync`, and `unique()` is `Values.Unique`.

## Package map

| JavaScript | .NET |
| --- | --- |
| `e2e` and `@e2e-dev/web` | `E2E` (`WebEngine` is Playwright, in the same package) |
| NUnit `[Test]` | `E2E.NUnit.E2ETest`. The fixture commits the replay cache from the NUnit result |
| `e2e.config.ts` | `E2EConfig.Load` reads `e2e.config.json`. Nothing in the SDK or `E2ETest` reads the result yet, and its keys differ from upstream ([#21](https://github.com/hardkoded/e2e-dotnet/issues/21)) |
| Vercel AI SDK model | `OpenAiCompatibleModel` (chat completions and tool calls) |
| — | `DocumentEngine`, an in-memory page for hosts that do not want a browser |

## Ported

- NUnit `[Test]`, `[SetUp]`, `[TearDown]`, `[OneTimeSetUp]`, and `[OneTimeTearDown]`
- `Assert.Ignore`, `[Retry]`, `[Timeout]`, and `[Category]`
- Screen queries: role, text, label, test id, placeholder, `filter({ hasText })`, `first`, `nth`. Text match is a string only. Regex, the role state options, `visible`, `last`, `filter({ has })`, `getByDisplayValue`, and chained queries other than `GetByRole` are missing ([#15](https://github.com/hardkoded/e2e-dotnet/issues/15))
- Actions: tap, fill, press, check, uncheck, clear. They wait up to the action timeout for exactly one enabled match
- Locator reads: `textContent` and `inputValue` only
- Locator expectations: visible, hidden, text, count, enabled, disabled, checked, value. They poll until the assertion timeout. `.not`, per-matcher timeouts, regex, and the other matchers are missing ([#18](https://github.com/hardkoded/e2e-dotnet/issues/18))
- `agent.act`, `agent.assert`, `agent.waitFor`, `agent.extract`, with fewer options and result fields than upstream ([#13](https://github.com/hardkoded/e2e-dotnet/issues/13)). `act` takes `params`, `timeout`, and `maxModelCalls`, and returns a summary and cache info. `assert` takes `timeout`. `waitFor` takes `timeout` and `interval`. `extract` takes no options. The act tools have no `observe`, `back`, `scroll`, or `scroll_to` ([#14](https://github.com/hardkoded/e2e-dotnet/issues/14))
- Replay cache for a verified `act`: role, name, test id, and path. Modes are `self-finalized`, `agent-concluded`, and `missed`
- `Values.Unique` and `Secret`. Secret values are redacted from snapshot text with an exact, case-sensitive match. Other casings, encodings, the statement text of `assert`, `waitFor`, and `extract`, and engine error messages are not redacted yet ([#7](https://github.com/hardkoded/e2e-dotnet/issues/7))
- OpenAI-compatible tool calling

A passing locator expectation or `agent.assert` after `act` writes the recording. `agent.assert`, `waitFor`, and `extract` always run live. Retries do not replay and do not record. Upstream retries skip replay but still record ([#23](https://github.com/hardkoded/e2e-dotnet/issues/23)).

## Intentional differences

Defaults differ from upstream. [#23](https://github.com/hardkoded/e2e-dotnet/issues/23) tracks whether to align them.

| Setting | Upstream | .NET |
| --- | --- | --- |
| Test timeout | 120 s | 60 s |
| Action timeout | 30 s | 5 s |
| `maxModelCalls` | 25 | 12 |
| `waitFor` interval | 3 s | 200 ms |

Error codes differ for locators. A locator with no match fails with `NOT_FOUND` (upstream: `LOCATOR_NOT_FOUND`). A locator with more than one match fails with `STRICT_MODE` (upstream: `LOCATOR_AMBIGUOUS`).

## Not ported

- The `e2e` command-line tool, the custom runner, `[E2ETest]` discovery, `--grep`, `test.only`, and the JSON report
- `@e2e-dev/mobile`, `@e2e-dev/github`, `@e2e-dev/kernel`, `@e2e-dev/eas`
- MCP server, `e2e init`, `e2e login`, OAuth subscriptions (ChatGPT, Copilot, Grok)
- Vision, screenshots, traces, and video
- Parallel workers, serial suites, project tools, and custom executors
- The rest of the locator action set: `click`, `selectOption`, `pressSequentially`, `focus`, `scrollIntoView`, `doubleTap`, `longPress`, `secondaryTap`, `dragTo`, `swipe`, `hover`, `setInputFiles`, pointer points, and `fill(Secret)` ([#17](https://github.com/hardkoded/e2e-dotnet/issues/17))
- The rest of the locator reads and waits: `getAttribute`, `isVisible`, `isHidden`, `isEnabled`, `isDisabled`, `isChecked`, `count`, `all`, `allTextContents`, `boundingBox`, `locator.waitFor`, and the per-action `timeout` option ([#16](https://github.com/hardkoded/e2e-dotnet/issues/16))
- Value expectations, `expect.poll`, and `expect.soft` ([#19](https://github.com/hardkoded/e2e-dotnet/issues/19))
- Agent options `judge`, `system`, `context`, `providerOptions`, `maxInputTokens`, `maxSteps`, `judgmentTimeout`, and named agents ([#13](https://github.com/hardkoded/e2e-dotnet/issues/13))
- The `browser` fixture, and `app.restart`, `app.clearState`, `app.back`, and `app.baseUrl`. `App` only has `OpenAsync` ([#20](https://github.com/hardkoded/e2e-dotnet/issues/20))
- Config: the upstream config shape, cache modes, `secrets` and `E2E_SECRET_<NAME>`, `launchTimeout`, and `cleanupTimeout` ([#21](https://github.com/hardkoded/e2e-dotnet/issues/21))
- Route patterns that ignore a record id. This port compares the URL path exactly and ignores the query and fragment
- Diff-only observations. Each model turn receives a full text snapshot
- Telemetry
- The upstream reporter, GitHub pull request comment, and trace viewer

## Web engine

`WebEngine` launches Chromium through Playwright and builds a semantic tree in the page: explicit roles, common implicit roles, accessible name, text, `data-testid`, disabled, checked, and hidden. `disabled` and `checked` come from the native properties only. `aria-disabled` and `aria-checked` are not read yet ([#3](https://github.com/hardkoded/e2e-dotnet/issues/3)). The role table, the `expanded`, `selected`, `pressed`, and `focused` states, shadow DOM, iframes, and options such as `viewport` and `testIdAttribute` are not complete ([#22](https://github.com/hardkoded/e2e-dotnet/issues/22)). Each observed element keeps a stable ref for the life of the document, and actions run on the element that ref names. Firefox and WebKit launch options are not exposed yet; the package reference can drive them later.

Password fields and fields with `autocomplete="current-password"` are marked secure. Their values are omitted from the snapshot.
