# Compatibility

Upstream parity target: [tester-army/e2e](https://github.com/tester-army/e2e) commit `f7c075666672d128f79ef2ec347fda67ea9e42ce` (`fix(expect): waitFor attached/detached, absent-frame absence, toHaveProperty primitives, toHaveURL ignoreCase`), recorded from the default branch on 2026-10-02.

Names are C# versions of the JavaScript API: `agent.act` is `ActAsync`, `screen.getByRole` is `GetByRole`, `expect(locator).toContainText` is `Expect.That(locator).ToContainTextAsync`, and `unique()` is `Values.Unique`.

## Package map

| JavaScript | .NET |
| --- | --- |
| `e2e` and `@e2e-dev/web` | `E2E` (`WebEngine` is Playwright, in the same package) |
| NUnit `[Test]` | `E2E.NUnit.E2ETest`. The fixture commits the replay cache from the NUnit result |
| `e2e.config.ts` | `e2e.config.json`, the same keys as JSON. `E2ETest` finds and applies it; fixture properties override it |
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

## Config

`E2ETest` loads the nearest `e2e.config.json` above the test assembly directory, then above the working directory, once per run. Supported keys: `targets` (one entry with `name`, `platform`, and `app.url`), `timeout`, `launchTimeout`, `actionTimeout`, `assertionTimeout`, `cleanupTimeout`, `retries`, `agents`, `cache` (`mode`, `dir`, `strict`), and `secrets`. Defaults, the CI cache demotion to `read-only`, `E2E_SECRET_<NAME>` overrides, the 6 code point secret minimum, and `INVALID_CONFIG` for unknown keys follow upstream. Keys of the old .NET shape (`app`, `engine`, `agent`, `timeouts`) fail with a hint to the upstream key.

Differences:

- JSON has no engine handles or model instances. The fixture's `CreateEngine` chooses the engine, `platform` must be `web`, and `targets` holds one entry. `agents.<name>.model` is an OpenAI-compatible model id, with the .NET-only `baseUrl` and `apiKeyEnv` beside it. Only `agents.default` is used
- A secret set to `null` reads only `E2E_SECRET_<NAME>`. Provider functions do not exist in JSON. A test reads secrets with `Secrets.Get(name)`
- `retries` is validated and resolved (1 in CI, 0 elsewhere) but NUnit retries still come from `[Retry]`
- `cache.strict` treats any replay that finds a recording but does not finish it as stale, and leaves that recording in place
- A launch or cleanup timeout fails with `ENVIRONMENT_UNAVAILABLE`
- Not supported, and rejected with `INVALID_CONFIG`: `projectId`, `tests`, `failOnSkippedFailure`, `workers`, `artifacts`, `output`, `trace`, `video`, `reporters`, `credentials`, `cache.store`, the non-URL `app` keys, and agent keys other than `model` and `maxModelCalls`. Credentials still come from `E2E_USER_<NAME>_USERNAME` and `_PASSWORD`

## Web engine

`WebEngine` launches Chromium through Playwright and builds a semantic tree in the page: explicit roles, common implicit roles, accessible name, text, `data-testid`, disabled, checked, and hidden. Each observed element keeps a stable ref for the life of the document, and actions run on the element that ref names. Firefox and WebKit launch options are not exposed yet; the package reference can drive them later.

Password fields are marked secure and their values are omitted from the snapshot.
