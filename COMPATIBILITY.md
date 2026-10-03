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
- `Values.Unique` rejects an empty or whitespace value, and a value that contains the cache slot marker (U+0001, the port's form of `{{param:`)
- A secret value has at least 6 code points. `Secret.Create` throws `INVALID_ARGUMENT`, and `Credentials.User` throws `INVALID_CONFIG` for a short `E2E_USER_*_PASSWORD`
- Agent budgets: `ActOptions.MaxSteps` (default 25), `ActOptions.MaxModelCalls`, and `WaitForOptions.MaxModelCalls`. A per-call budget can only lower the configured one (`E2ESessionOptions.MaxSteps`, `MaxModelCalls`). A higher or non-positive value throws `INVALID_ARGUMENT`
- An `act` past its action budget ends `STEP_BUDGET_EXHAUSTED`, blocked. The model is told and may still conclude: a passing verdict, or a failure without a code, becomes the budget error. Replayed actions draw on the same budget
- `ActResult.ModelCalls` (0 for a full replay) and `ActResult.Actions` (replayed and live actions, counting failed attempts)
- `AgentException.Blocked` and `AgentException.Explanation` (the same text as `Message`)
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

### Agent options not yet ported

Tracked in [#13](https://github.com/hardkoded/e2e-dotnet/issues/13).

- `ExtractOptions`: a Standard Schema `schema`, a per-call `timeout`, and validation with one repair round before `MODEL_OUTPUT_INVALID`. `ExtractAsync<T>` deserializes into `T`
- `judge` (a separate judgment model), `system`, and `context`
- Named agents (`agents.<name>` and the per-call `agent` option)
- `providerOptions`, `judgmentTimeout`, `maxObservationBytes`, and `maxInputTokens`
- `vision` and `screenshot` on judgments, and `AgentError.screenshot`
- Act param limits: 64 KiB and 32 levels deep. A cycle is already rejected with `INVALID_ARGUMENT` when the cache key serializes the params
- Range checks on the configured budgets (1 through 100). The port's default `MaxModelCalls` is 12, upstream's is 25
- A model-call budget exhausted by `act` ends `STEP_NO_CONCLUSION`, not `STEP_BUDGET_EXHAUSTED`

## Web engine

`WebEngine` launches Chromium through Playwright and builds a semantic tree in the page: explicit roles, common implicit roles, accessible name, text, `data-testid`, disabled, checked, and hidden. Each observed element keeps a stable ref for the life of the document, and actions run on the element that ref names. Firefox and WebKit launch options are not exposed yet; the package reference can drive them later.

Password fields are marked secure and their values are omitted from the snapshot.
