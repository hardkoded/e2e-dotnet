---
name: create-verification-skill
description: Generate a project-local verify-<app> skill on top of the .NET port of e2e, so any coding agent can start the app, check it is worth driving, exercise a feature the way a user does, keep evidence, and hunt for bugs. Writes the skill, a feature map with one bug-bash goal per feature, and proves it once. Use for /create-verification-skill, "make a verification skill for this repo", or when a project has no scripted way to prove UI behavior.
disable-model-invocation: true
---

# Create a verification skill

A coding agent that changes an app should be able to prove the change the
way a user would see it. This skill writes that proof recipe for one
project as a skill, `.agents/skills/verify-<app>/`, built on e2e. The
config names the app, `e2e mcp` drives it live, NUnit or xUnit tests pin
what must hold, repro tests turn a suspected bug into a proof, and the
test output keeps the evidence. Write the output for the next agent, not
for a human. It will be read cold, mid-task, by an agent that has never
seen the app.

The e2e skill is the reference for everything e2e does. Read it first and
link to it rather than restate it. It is the `e2e-dotnet` skill, and
`e2e guide <topic>` prints it from the installed `E2E.Cli` tool. Topics
named below (`setup`, `writing-tests`, `writing-tests-nunit`,
`writing-tests-xunit`, `mcp`, `bug-bash`, `running`, `debugging`) are its
reference files.

In the .NET port there is no `e2e init`, `e2e run`, or `e2e list`, and no
run report. Tests run with
`dotnet test`. The sections below say what stands in for each.

## Contents

- 1. Interview the repo, not the user
- 2. Generate the skill
- 3. Seed the feature map
- 4. Prove the generated skill before handing it over
- 5. Hand over
- Rules

## 1. Interview the repo, not the user

Answer these from the codebase and ask the user only what you cannot
observe.

| Question | Where the answer is | What it decides |
| --- | --- | --- |
| Surface | What a user touches. A web UI, an iOS or Android app, an API | One skill per primary surface; name the others for a later run. A web UI is the config's web target. A mobile app is driven by `MobileEngine` in code (topic `setup`); `e2e mcp` also drives the documented mobile and Windows targets (topics `mobile` and `windows`). An API with no UI has no engine to drive; tests call it with `HttpClient`, and the skill says so. A Windows desktop app is driven by `WinAppEngine` through a `windows` target (topic `windows`), which `e2e mcp` also drives on Windows; a desktop app on another OS has no e2e engine, say so and stop |
| Run | The documented dev command, its ports, env vars, seed data, auth | The command the generated skill tells the reader to start by hand, or the target's `app.command`, which starts the app and polls `app.readyUrl` (default `app.url`) before the first test, `e2e explore`, or `open_session` needs it (topic `setup`). A dev server that compiles a route on first visit reads as broken to an agent, so prefer a production build command when the repo has one |
| e2e state | `e2e.config.json`, a test project that references `E2E.NUnit` or `E2E.XUnit.V3`, the `E2E.Cli` tool, `.agents/skills/e2e-dotnet/`, the MCP server in `.mcp.json` or `.cursor/mcp.json`, and any Playwright or Cypress suite | Nothing there means topic `setup` first, then `claude mcp add e2e -- e2e mcp` or a `.mcp.json` entry (topic `mcp`). An existing Playwright or Cypress suite is the best source of locators, seeds, and sign-in steps; read it. A config with no model runs the deterministic steps and the MCP tools, and nothing else |
| Observe | Screenshots, the `dotnet test` log, and what the app leaves behind in files, rows, sent messages, logs | The side-effect stores the Evidence section names. Find them now |
| Isolate | Can two instances run side by side? | Tests share one `app.url`, so two runs against one app share its data. Use `Values.Unique(...)` for data a test creates. A shared instance that cannot be duplicated (one database, one simulator) becomes a rule in the generated skill. Refusing to double-drive it beats corrupting the user's session |
| Name | The package name or the repo name | `<app>` in `verify-<app>`, lowercase, no scope |

If the checkout does not build or start as-is, fix that first or report it
precisely before generating. A skill written against a broken base teaches
wrong steps. An irrelevant missing asset (a static dir the API never
serves, a sample config) may be created by the generated skill, marked as
verification scaffolding and removed in Cleanup.

An existing `verify-<app>/` is read before anything is written. Keep its
SKILL.md and feature files, add the features the map lacks, and change an
existing file only where the app contradicts it.

## 2. Generate the skill

Write `.agents/skills/verify-<app>/SKILL.md` with YAML frontmatter.
`name: verify-<app>` and a `description` naming the app, the surface, and
when to reach for it; without frontmatter the skill never registers. When
the project has `.claude/skills/`, link it with a relative symlink
`.claude/skills/verify-<app>` to `../../.agents/skills/verify-<app>`.
Every section says what the interview found, no placeholders.

| Section | Holds |
| --- | --- |
| Read first | One line that sends the reader to the e2e skill and names the config file and test project this skill drives |
| Launch | The exact command that starts the app for verification and how readiness shows: the line that proves the port answers. Say which ports and env the command needs and what seeds the data. A target whose `app.url` is a hosted site needs no launch; say so |
| Doctor | One read-only check that answers "is this instance worth driving?". The smoke test is a model-free test that opens the app and pins one landmark with `Expect.That`, kept out of the `RealModel` category; `dotnet test --filter "FullyQualifiedName~SmokeTests"` exits `0` when the app answers, the engine runs, and the landmark renders. No smoke test in the project: write one now. A signed-in app's auth doctor is its sign-in test. An agent runs Doctor first whenever anything looks off |
| Drive | Two lanes and when to use each. Live over MCP: `open_session`, `observe`, the verbs, and `locate` before any locator is written (topic `mcp`); list the credentials the session offers through `type_secret`. Scripted: a test class in the test project, `Screen` where the name on screen is known, `Agent.ActAsync` where the flow varies (topic `writing-tests`, and `writing-tests-nunit` or `writing-tests-xunit` for the framework the project uses). Prefer stable handles, roles and accessible names, labels, test ids, routes. Name the credentials a signed-in drive starts from (`Credentials.User(name)`) and the environment variables that hold them, `E2E_USER_<NAME>_USERNAME` and `E2E_USER_<NAME>_PASSWORD`, never their values |
| Evidence | What a proof keeps and where. The port writes no test report. A trace-enabled attempt writes `.e2e/results/<result>/trace.md` when its trace mode keeps it (`retain-on-failure` keeps failed attempts, `on` every attempt; topic `debugging`), and a video is written only when video recording is on. The proof is the `dotnet test` result (`--logger "trx;LogFileName=<name>.trx"`, topic `running`) and what the test pins: `Expect.That(...)` on the end state, and `ToHaveScreenshotAsync` for a stored screenshot (topic `writing-tests`). The next run reuses `.e2e/` for the replay cache; copy a result out before a run that overwrites it. State the proof standards (below) |
| Bug bash | How this app is bashed (topic `bug-bash`). The bug bash fan-out is not ported, so the skill has no fan-out: the agent walks a mapped feature live over MCP or with a short `Agent.ActAsync` test, using the goals in the feature map. The verification bar: a finding is a bug only once a repro test fails with `ASSERTION_FAILED` on the assertion that encodes it, in a test project folder named for bug-bash repros |
| Cleanup | What to tear down. `close_session` stops a session's browser; an app the user started stays theirs. Never kill by process name; stop what you started. Cleanup removes instances and scratch state (seeded accounts, scaffolding, repro tests whose findings were all rejected), never the evidence; name where the evidence is |
| Helpers | Any script the skill ships is executable and its invocation is in the skill body. A helper the reader has to reverse-engineer is not a helper. Most projects need none; `dotnet test` and the `e2e` tool are the helpers |

Proof standards, for the Evidence section: exercise the real user path,
not internal setters or test-only endpoints; capture the action and the
resulting state, not only the final screen; verify side effects (files
written, rows inserted, messages sent) in the stores the interview found;
mock only where a production boundary already isolates the external
system. When the safe path is a dry run or a test mode, verify what it
skips by watching files, network, and refs rather than trusting its name.

## 3. Seed the feature map

Create `.agents/skills/verify-<app>/features/README.md` plus one file per
user-facing feature you can identify, the top three to five first, from
routes, navigation, commands, or docs. Follow
[references/example/verify-todomvc/](references/example/verify-todomvc/),
the skill this generator produced for the TodoMVC sample (`samples/TodoMvc`).
The index is a table of feature, route, test class, and status (`tested` or
`untested`). Each feature file has these H2s, from the user's point of
view.

| H2 | Holds |
| --- | --- |
| `Sub-features` | What the feature is made of, one line each |
| `How to get to it (user POV)` | Route, navigation, the account it needs |
| `Driving it with e2e` | The test class that pins it, the locators that matter (checked with `locate`), the MCP steps for a live walk |
| `Proof` | The observable end state that means it works, on screen and in the side-effect store |
| `Charters` | One or two bug-bash goals for this feature, each one area, one posture, one sentence (topic `bug-bash`). Written as the line `slug\|target\|charter`, so a later agent can paste them. A goal that starts signed in names the credential it needs and runs alone |
| `Gotchas` | What reads as broken but is not; what the local app cannot do |

The map is the project's maintained verification source, and the bug bash
reads its charters. A proof that drives one convenient entry point is
incomplete when the map lists others, and a bug bash that skips a mapped
feature reports the gap. A feature without a test gets one in step 4 or is
marked `untested` in the index.

## 4. Prove the generated skill before handing it over

Run its own instructions end to end once, from a shell in the project.

1. Doctor: start the app the way Launch says, then run the smoke test; it
   exits `0`.
2. Drive one mapped feature through its test class. One is enough; the map
   exists so later runs cover the rest. A missing test: write it now from
   the feature file, with locators checked over MCP.
3. Bug bash one charter from the map: walk it live over MCP, or in a short
   `Agent.ActAsync` test, and triage what you find. Do not verify every
   finding now. This proves the model and the walk, not the app. Without a
   model, skip this step and say in the handover that the bug bash is
   unproven.
4. Cleanup, then confirm the evidence still exists at the named location.
   A cleanup that eats the proof fails this step.

Fix what fails, and run Cleanup after every failed iteration too, so
broken attempts do not strand ports and processes. A generated skill that
was never executed is a draft, not a deliverable. Leave the project as the
user had it. The generated skill, its feature map, and any test you added
are the change; `.e2e/` output stays untracked unless the user asks
otherwise.

## 5. Hand over

Tell the user what landed. The skill path, the features mapped and which
have tests, the charters, what the proof run showed, and the evidence
paths. Running the map keeps it honest. When a route or label changes and
a test fails, the feature file changes in the same commit. Rerun this skill
when the app grows a feature the map lacks; section 1 says how an existing
map is treated.

## Rules

- Generate from what you observed. A section that could apply to any app
  is not finished.
- One sentence from the e2e skill beats a paragraph of your own. Link the
  topic.
- Never put a secret in the generated skill or the map. Name the
  credential (topic `setup`).
- Do not commit on the user's behalf. List what you created.
