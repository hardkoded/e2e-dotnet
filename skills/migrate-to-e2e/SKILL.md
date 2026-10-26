---
name: migrate-to-e2e
description: Moves an existing Playwright .NET, Selenium .NET, Appium, Cypress, Detox, or Maestro suite to the .NET port of e2e one slice at a time, with the old suite running beside the new one until each ported test is proven. Keeps a MIGRATION.md progress file so any later session resumes where the last stopped. Optionally plants a defect to show both suites catch it. Use for /migrate-to-e2e, "migrate our tests to e2e", "port these Playwright tests", or when a project has an e2e test project and an older suite still running.
disable-model-invocation: true
---

# Migrate a suite to e2e

A migration is never one change. Each session ports one slice, proves it,
and leaves both suites green: the old runner keeps every test it had, and
the new e2e tests pass beside it. A progress file, `MIGRATION.md`, records
what moved, what is left, and why, so the next session, yours or another
agent's, starts from it instead of from scratch. The user decides when an old test goes.

This is the .NET port's version of upstream's `migrate-to-e2e`. The e2e
tests are NUnit or xUnit v3 tests that derive from `E2ETest`, run with
`dotnet test`. The port has no `e2e init`, `e2e run`, `e2e list`, or
`e2e feedback`, and no per-runner migration guides.

One reference does most of the work. Read it; do not restate it.

- The e2e skill: `.agents/skills/e2e-dotnet/SKILL.md` once
  `npx skills add hardkoded/e2e-dotnet --skill e2e-dotnet` ran, or `e2e guide <topic>` from the
  installed `E2E.Cli` tool. Topics named below (`setup`, `writing-tests`,
  `writing-tests-nunit`, `writing-tests-xunit`, `agent`, `mcp`, `running`,
  `debugging`, `mobile`, `windows`) are its reference files.

Detect the source runner, then read the topics in its row in full, before
the inventory and before writing any code.

| Source | Detect it by | Engine topic |
| --- | --- | --- |
| Playwright .NET | `Microsoft.Playwright.NUnit`, `Microsoft.Playwright.Xunit`, or `Microsoft.Playwright` in a `.csproj` | `setup` (web) |
| Selenium .NET | `Selenium.WebDriver` in a `.csproj` | `setup` (web) |
| Playwright or Cypress (Node) | `playwright.config.*`, `cypress.config.*`, a `cypress/` folder | `setup` (web) |
| Appium, Detox, Maestro | `Appium.WebDriver` in a `.csproj`, `.detoxrc*`, YAML flows opening with `appId:` and `---` | `mobile` |

Also read `writing-tests-nunit` when the e2e project uses NUnit, or
`writing-tests-xunit` when it uses xUnit v3. Follow the source project's
framework when it is a .NET one; otherwise ask the user.

No guide maps a runner's API to e2e. Use the e2e skill's `writing-tests`
topics and the locator table in `setup`, and tell the user the mapping is
by hand. A project with two source runners migrates one at a time, one
section of the progress file each.

## 1. Resume or start

Look for `MIGRATION.md` beside the e2e test project, then at the repo root.
When one exists, this is a later session:

1. Read it. Its `Next` section names the slice this session takes, unless
   the user names another.
2. Check it against the repo before trusting it: every `ported` test still
   exists in both suites, every source file or test it lists still exists
   (except `retired` ones, deleted on purpose), and no new source file or
   test is missing from it (add those as `todo`). No `Defect check:` line:
   ask as in step 2. A planted bug noted under `Next`: revert it first
   (step 6).
3. Run the file's `Start dependencies` command and check the services are
   ready, then the `Run new` command once. A ported test that fails now is
   the first thing to fix, before any new slice.
4. Unless the progress file's `Harness:` line says `done`, go to step 3,
   Build the harness. Otherwise go to step 4, Pick the slice.

No progress file means a first session: steps 2 and 3, then the rest.

## 2. Inventory the suite (first session)

Answer these from the repo; ask the user only what you cannot observe.

| Question | Where the answer is | What it decides |
| --- | --- | --- |
| Source runner | The detection table above | The topics read before this table |
| Tests | Every test class or flow file, with its names, categories, and per-browser or per-platform branches. Count with the runner's own list, not by grepping, which misses `[TestCase]` and `[Theory]` rows or tests made in a loop: `dotnet test --list-tests` for a .NET suite, `npx playwright test --list` for Playwright, `npx cypress run --dry-run` is not available so list Cypress `*.cy.*` specs and count their `it(` calls, the `.detoxrc*` `testRunner` list for Detox, and one test per YAML flow file for Maestro (`maestro test --help` has no dry run). Count a test once however many cases, projects, or browsers run it | The progress file's rows, one per source file. Count each test's assertions now so a port can be checked against the count: explicit ones (`Expect(...)`, `Assert.That`, `Assert.Equal`, `assertVisible`), plus, where the runner's steps are its checks (a Maestro `tapOn` fails when its target is missing), those steps |
| Shared code | Page objects, base classes, fixtures, `[SetUp]` and `[OneTimeSetUp]`, subflows | Helpers to port once, before the first test that needs each |
| How it runs | The CI job that runs the suite and the scripts it calls: the build, the services and mock servers it starts, seed and reset calls, env files, devices or browsers, ports | The e2e harness. The CI job is usually the only complete description; read it line by line |
| Where e2e lives | A new test project beside the old one (`MyApp.E2ETests`), referencing `E2E.NUnit` or `E2E.XUnit.V3` (or the `.Mobile` ones) | The config's location and the progress file's. The two runners must not collect each other's tests: they are separate projects, so `dotnet test` on the solution runs both. Check each project's test list |
| Can the old suite run here | The source runner installed, the build it needs, the services | Whether parity is checked against a live old run (step 6) or only against the source text |

Write `MIGRATION.md` now (format in step 8), every source file `todo`.

Ask whether each slice should also run against a planted defect (step 6)
and record the answer as `Defect check: on` or `off`.

## 3. Build the harness (once)

The harness is the config, the app launch, and one green test. It is done
once and the progress file says so; later sessions skip this step.

1. No e2e test project: create one (topic `setup`), in a new folder beside
   the old suite, or at the repo root of a Node-only repo (`e2e/` next to
   its `package.json`, with the .NET project inside), and run it in CI as its
   own `dotnet test` step: add `E2E.NUnit` or
   `E2E.XUnit.V3` (`E2E.Mobile.NUnit` or `E2E.Mobile.XUnit.V3` on a device
   project), write `e2e.config.json`, and add one example test. Delete or
   rewrite the example. One target per platform or browser the source suite
   covers.
2. Ask the user which model agent steps use, and configure it (topic
   `agent`). A literal port needs none: every test can be deterministic, and
   a config without a model runs those.
3. Port the orchestration the CI job does, reusing the repo's own scripts
   rather than writing new ones:
   - The port has no `app.command`. A web target's app must answer on its
     `app.url` before `dotnet test`, so the harness starts it the way the
     old suite's CI job did (a script, a `WebApplicationFactory`, a
     fixture that starts a process). Name the command in the progress file.
     A device target has no URL: `MobileAppTest` launches its app, so skip
     this step for it (topic `mobile`).
   - Databases, queues, and mock servers start before the run with the
     command the CI job uses.
   - A reset or seed the source ran per test becomes a call in the test's
     `[SetUp]` or `InitializeAsync`, so each test still starts from known
     data. A seed helper that references the app's own code is reused, not
     rewritten.
   - Env values become constants or `Environment.GetEnvironmentVariable`
     reads. A login account is not written in a test or in the config:
     set `E2E_USER_<NAME>_USERNAME` and `E2E_USER_<NAME>_PASSWORD` in the
     environment and read it with `Credentials.User(name)` (topic `setup`).
     The config has no `credentials` key. Other secrets go under `secrets`
     with `null`, read from `E2E_SECRET_<NAME>`.
4. Port the simplest source test, often the sign-in or the launch, as the
   smoke test. It must pass on every target before any other port. When it
   cannot, the harness is wrong, not the test: fix the harness.
5. Keep the old suite's project and scripts as they were.
6. Mark the harness done in the progress file, with the commands that start
   the dependencies, run the old suite, and run the new one.

## 4. Pick the slice

A slice is what this session can finish green: one class, one folder, one
category, or one feature area, usually one to five tests. Ask the user
which. When they leave it to you, suggest in this order:

1. Tests whose shared helpers are already ported.
2. Tests that break often in the old runner: retries, flaky history in CI,
   comments explaining a workaround for the runner. They gain the most.
3. Tests that need a capability the port lacks, last (read `COMPATIBILITY.md`).

Write the slice into the progress file's `Next` section before starting, so
an interrupted session leaves a trail.

## 5. Port the slice

Port the slice's shared helpers first, once, as plain classes in the e2e
test project. A subflow becomes a method; a page object becomes a class
over `Screen` (topic `writing-tests`). Record each in the progress file.

Then each test, one at a time:

- Keep its name, and open the class with a comment naming the source
  (`// Ported from Tests/LoginTests.cs`), so reports and reviewers map one
  to the other.
- Translate literally first, one awaited call per source step. Each source
  assertion maps to its own e2e assertion (`Expect.That(...)`). A wait that
  fails on timeout becomes one more, so the e2e count is at least the
  inventory's.
- A test the source ignores (`[Ignore]`, `[Fact(Skip = ...)]`) is ported in
  full and stays ignored, with the source's reason, so it can be turned back
  on in either suite. Its row says so; when the reason allows, run it
  unignored once and note the result.
- Learn exact names before writing a locator: `e2e mcp` with `open_session`,
  `observe`, and `locate` (topic `mcp`). When your client has no `e2e` MCP
  tools, run the test and read the failure (topic `debugging`); do not build
  a bridge. e2e matches whole strings and fails on several matches where the
  old runner took the first. When a query also matches a hidden copy, add
  the visible option; when two visible nodes match, pick the one the step
  means.
- A sleep or fixed wait becomes `Agent.WaitForAsync` on the state it waited
  for, or an assertion with a timeout when a locator names that state or
  the config has no model. Never port the duration.
- A per-platform branch becomes a target per platform, or an `if` on the
  platform inside one test.
- A workaround the source comments explain (a flaky `hideKeyboard`, a
  coordinate tap, a retry loop) is a workaround for the old runner. Try the
  plain step first; keep a workaround only when e2e needs it too, with its
  own comment saying why.
- Where the port lacks a feature, the test stays in the old runner: mark it
  `stays` with the reason, and offer to open an issue at
  `hardkoded/e2e-dotnet` with the gap. Open it only when the user agrees.
- Do not change app code. A test that needs a test id the app lacks gets
  `Agent.ActAsync` or a different locator; propose the app change to the
  user instead of making it. A defect check's planted bug (step 6) is the
  exception, reverted the same session.

`Agent.ActAsync` comes after the literal port passes, never instead of it,
and only with a model configured. Propose it for a stretch the source kept
patching: coordinate taps, picker spins, long tap sequences through a
wizard. Pin the outcome right after with a locator assertion (topic `agent`).

## 6. Prove each port

A port is done when it is as strict as the source and passes every time.

1. Run it on every target the source covered, three times in a row, with
   `dotnet test --filter` on the test (topic `running`); the port has no
   `--repeat-each`. When it has agent steps, also run it once with
   `"cache": { "mode": "off" }` in `e2e.config.json` (topic `agent`), then set
   the mode back. Three of three on each target, or it is not done.
2. When the old suite can run here, run the source test on the same build.
   A failure both runners show is an app or environment bug: report it, do
   not port around it. A failure only e2e shows is the port's.
3. Check strictness against the source: an e2e assertion for every source
   assertion, so the count is at least the inventory's, and every end state
   the source checked is checked. Never weaken an assertion, widen a timeout
   without a reason, or drop a step to go green. A test that cannot pass
   honestly stays `blocked` with what failed.
4. Read the failure output of any failing test before changing the test
   (topic `debugging`).

### Defect check (optional)

With `Defect check: on`, show both suites fail on the same bug:

1. With the user's agreement, plant a small bug in an end state the source
   asserts (a checkout showing the wrong total), in a file with no
   uncommitted changes. Note the file under `Next` until it is reverted.
2. Rebuild, confirm the app shows the bug, then run `Run old` on the slice
   (when it runs here) and the e2e tests of the slice with the cache off.
3. Revert only that file (`git checkout -- <file>`), rebuild, and confirm
   `git diff -- <file>` is empty.
4. Both fail at the step that checks the planted state: record it. Only the
   old suite passes: record that the source was weaker. Only e2e passes: the
   port lost an assertion, so it is not done. Neither fails: pick another
   bug. A failure before that step is not a catch.
5. Fill the row's `Defect check` cell, for example `total +1: both fail`, or
   `old not run` when the old suite cannot run here.

## 7. Run both suites

Until the user retires a source test, both runners keep it.

- Ported tests stay in the old suite too. Delete a source test only when the
  user confirms, then mark it `retired`. Once neither a remaining source
  test nor e2e code uses a source helper, propose deleting it, and delete it
  when the user confirms.
- CI: propose adding the e2e project beside the old job once a few ports are
  green, and make the change only when the user agrees, since it edits the
  old runner's workflow. Reuse its build, services, and devices. Two
  runners never drive one device or simulator at the same time; run them one
  after the other in the same job, or give each its own device.
- When the last source test is retired or marked `stays`, the progress file
  says the migration is complete and which tests, if any, stay behind.

## 8. Keep the progress file

`MIGRATION.md`, beside the e2e test project, is the migration's state.
Update it as you go, not at the end; an interrupted session must leave it
true.

```md
# Migration to e2e

Source: Playwright .NET, `MyApp.Tests/**`
e2e: `MyApp.E2ETests/e2e.config.json`, NUnit
Start dependencies: `docker compose up -d db`
Run old: `dotnet test MyApp.Tests`
Run new: `dotnet test MyApp.E2ETests`
Harness: done, smoke `LoginTests.Signs_in` on chromium
Defect check: on

| Source | e2e | Status | Assertions (source -> e2e) | Passing | Defect check | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| LoginTests.cs | LoginTests.cs | ported | 3 -> 3 | chromium 3/3 | wrong page: both fail | |
| CheckoutTests.cs | | blocked | 6 -> | | | date picker fails on webkit |
| SearchTests.cs | | todo | 4 -> | | | |

## Shared helpers

| Source | e2e | Used by |
| --- | --- | --- |
| Pages/LoginPage.cs | Support/LoginPage.cs | LoginTests, CheckoutTests |

## Gaps

- The source test uses a feature the port does not have; issue filed.

## Next

CheckoutTests.cs (needs `LoginPage`).
```

One row per source file. A file whose tests end in different statuses gets
one row per test, each with its own assertion count. A suite with hundreds
of tests keeps file rows: the table is read at the start of every session.

Statuses: `todo`; `ported` (green in e2e, the source test still runs);
`retired` (the user confirmed, the source test is deleted); `blocked`
(tried, does not pass yet, with what failed); `stays` (stays in the old
runner on purpose, with why).

## 9. Hand over

Tell the user what this session moved and what it proved: the tests ported
and their pass counts per target, defect check results, the helpers ported,
what is blocked or stays and why, the gaps reported, and the next slice.
Ask which `ported` source tests may be retired. List every file you created
or changed.

## Rules

- One slice per session. Leave both suites green: the old suite passes as it
  did, and every `ported` test passes.
- Never delete or edit a source test, helper, or the old runner's config
  without the user's confirmation.
- Port from what the repo shows. Run every command you put in the progress
  file at least once; one that cannot run here (the old runner not
  installed) says so in the progress file.
- A source step whose meaning is unclear, such as a tap on a disabled
  control or a coordinate tap that may miss, keeps its intent in the port.
  Note your reading in the progress file and ask the user.
- One sentence from the e2e skill beats a paragraph of your own. Name the
  topic.
- Never put a secret in a test, the config, or the progress file. Name the
  `E2E_USER_<NAME>_*` variables (topic `setup`).
- Do not commit on the user's behalf.
