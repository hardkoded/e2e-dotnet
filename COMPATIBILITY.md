# Compatibility

Upstream parity target: [tester-army/e2e](https://github.com/tester-army/e2e) commit `f7c075666672d128f79ef2ec347fda67ea9e42ce` (`fix(expect): waitFor attached/detached, absent-frame absence, toHaveProperty primitives, toHaveURL ignoreCase`), recorded from the default branch on 2026-10-02.

Names are C# versions of the JavaScript API: `agent.act` is `ActAsync`, `screen.getByRole` is `GetByRole`, `expect(locator).toContainText` is `Expect.That(locator).ToContainTextAsync`, and `unique()` is `Values.Unique`.

## Package map

| JavaScript | .NET |
| --- | --- |
| `e2e` and `@e2e-dev/web` | `E2E` (`WebEngine` is Playwright, in the same package) |
| NUnit `[Test]` | `E2E.NUnit.E2ETest`. The fixture commits the replay cache from the NUnit result |
| `e2e.config.ts` | `E2EConfig.Load` reads `e2e.config.json`. Nothing reads the result yet, and its keys (`engine`, `app`, `agent`, `cache`, `timeouts.*Ms`) differ from upstream (`targets`, `agents`, top-level timeouts). Set values by overriding `E2ETest` members |
| Vercel AI SDK model | `OpenAiCompatibleModel` (chat completions and tool calls) |
| — | `DocumentEngine`, an in-memory page for hosts that do not want a browser |

## Ported

- NUnit `[Test]`, `[SetUp]`, `[TearDown]`, `[OneTimeSetUp]`, and `[OneTimeTearDown]`
- `Assert.Ignore`, `[Retry]`, `[Timeout]`, and `[Category]`
- Screen queries: role, text, label, test id, placeholder, `filter(hasText)`, `first`, `nth`. Strings only, no regex. A chained locator only has `GetByRole`
- Actions: tap, fill, press, check, uncheck, clear. They wait up to the action timeout for exactly one enabled match
- Reads: `TextContentAsync`, `InputValueAsync`
- Locator expectations: visible, hidden, text, count, enabled, disabled, checked, value. They poll until the assertion timeout
- `agent.act`, `agent.assert`, `agent.waitFor`, `agent.extract`, with fewer options and result fields:
  - `ActOptions`: `Params`, `Timeout`, `MaxModelCalls`. No `maxSteps`
  - `ActResult`: `Summary`, `Cache`. No `modelCalls` or `actions`
  - `AssertOptions`: `Timeout`. `WaitForOptions`: `Timeout`, `Interval`. No `maxModelCalls`
  - `ExtractAsync<T>` takes no options. The shape comes from `T`, not a schema, and the timeout is the step timeout
- Replay cache for a verified `act`: role, name, test id, and path. Modes are `self-finalized`, `agent-concluded`, and `missed`
- `Values.Unique` and `Secret`. Snapshots and act prompts replace exact, case-sensitive matches of a secret value. Other casings and encodings, and `assert`, `waitFor`, and `extract` statement text, are not redacted (#7)
- OpenAI-compatible tool calling

A passing locator expectation or `agent.assert` after `act` writes the recording. `agent.assert`, `waitFor`, and `extract` always run live. Retries neither replay nor record.

## Differs from upstream

| | Upstream | .NET |
| --- | --- | --- |
| Test timeout | 120 s | 60 s |
| Action timeout | 30 s | 5 s |
| `maxModelCalls` | 25 | 12 |
| `waitFor` interval | 3 s | 200 ms |
| Locator matched more than one node | `LOCATOR_AMBIGUOUS` | `STRICT_MODE` |
| Locator matched no node | `LOCATOR_NOT_FOUND` | `NOT_FOUND` |

## Not ported

- The `e2e` command-line tool, the custom runner, `[E2ETest]` discovery, `--grep`, `test.only`, and the JSON report
- `@e2e-dev/mobile`, `@e2e-dev/github`, `@e2e-dev/kernel`, `@e2e-dev/eas`
- MCP server, `e2e init`, `e2e login`, OAuth subscriptions (ChatGPT, Copilot, Grok)
- Vision, screenshots, traces, and video
- Parallel workers, serial suites, project tools, and custom executors
- Screen query options: regex `TextMatch`, `getByRole` state options (`checked`, `disabled`, `selected`, `expanded`, `pressed`, `level`), `visible`, `getByDisplayValue`, `last`, `filter({ has })`
- Locator actions: `click`, `selectOption`, `focus`, `scrollIntoView`, `doubleTap`, `longPress`, `secondaryTap`, `pressSequentially`, `dragTo`, `swipe`, `hover`, `setInputFiles`, pointer points, and `fill(Secret)`
- Locator reads: `getAttribute`, `isVisible`, `isHidden`, `isEnabled`, `isDisabled`, `isChecked`, `count`, `all`, `allTextContents`, `boundingBox`, and `locator.waitFor`
- Agent model options: `judge`, `providerOptions`, `maxInputTokens`
- Route patterns that ignore a record id. This port compares the URL path exactly and ignores the query and fragment
- Diff-only observations. Each model turn receives a full text snapshot
- Telemetry
- The upstream reporter, GitHub pull request comment, and trace viewer

## Web engine

`WebEngine` launches Chromium through Playwright and builds a semantic tree in the page: explicit roles, common implicit roles, accessible name, text, `data-testid`, disabled, checked, and hidden. Each observed element keeps a stable ref for the life of the document, and actions run on the element that ref names. Firefox and WebKit launch options are not exposed yet; the package reference can drive them later.

Password fields and `autocomplete=current-password` fields are marked secure, and their values are omitted from the snapshot.
