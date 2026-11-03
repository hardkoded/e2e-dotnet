# Running a bug bash

A bug bash is many `e2e explore` runs at once, one charter each, then a
verification pass that turns every claimed bug into a repro test that fails
for the reason reported. Plan the charters, fan them out, merge the findings,
prove each bug. Report confirmed bugs only, each with its failing test.

## Contents

- 1. Prepare
- 2. Plan charters
- 3. Fan out
- 4. Merge
- 5. Triage
- 6. Verify
- 7. Report
- Rules

## 1. Prepare

- A config with a target and an agent that holds a model (topic `setup`),
  and authentication for its provider.
- The app must serve several explorers at once: start it once and set
  `reuseExisting: true` on the target's `app.command`, or declare the URL with
  port `0` so each run starts its own app on a free port. `reuseExisting` is
  ignored when `CI` is set, as in many agent sandboxes: use port `0` or
  unset `CI`. A fixed port that is not reused fails every run after the first.
- When the target's `app.command` brings up a stack of its own (a database
  it starts and removes on exit), the first explorer to finish removes the
  database under the rest. Start the stack once yourself with the config's
  command and environment, and explore with a bug-bash config that declares
  no command (below).
- Explore a production build when the project has one. A dev server that
  compiles a route on first visit reads to the explorer as a dead link.
- Seed one disposable workspace or user per charter with the project's own
  fixtures or seed scripts and declare each as a credential (`bb-<slug>`).
  Explorers that share an account report each other's edits as bugs.
- Put what the local app cannot do in the agent's `context`: integrations
  without keys, what is seed data, what must never be clicked (paid runs,
  real accounts). Add the explorer's own blind spots from step 5's artifact
  bucket (the config below carries the sentence); without them those
  families dominate the findings.
- A bash against a deployed site others use is read-only: no signups,
  sign-ins, or submissions, nothing injection-shaped in URLs, no request
  loops. A WAF block is the firewall working, not a finding, and it can
  follow the runner's IP into every later charter.
- Each exploration step needs 40 or more `maxSteps` and `maxModelCalls` on
  the agent; a config tuned for test steps (`maxSteps: 15`) starves it. That
  is per step, separate from `--max-steps`, which counts a charter's steps.
- On a mobile target, declare one target per device and give each explorer
  and verifier its own (topic `setup`).
- Skip `--headed`. On an engine that records, `--video` gives each run a
  replay for any confirmed bug.
- The port has no saved sessions: `e2e explore --session` exits 2, so an
  explorer starts signed out. A charter that needs an account names its
  credential and the explorer signs in with `fill_secret` (topic `explore`).

A bug-bash config, left untracked, is a copy of the project's
`e2e.config.json` with what a bug bash needs changed. JSON has no spread, so
copy the keys you keep (`targets`, `secrets`, `cache`) and write the rest.
Save it as `e2e.bugbash.config.json`:

```json
{
  "targets": [{ "name": "web", "platform": "web", "app": { "url": "http://127.0.0.1:3000" } }],
  "credentials": {
    "bb-cart": { "username": "bb-cart@example.test" },
    "bb-account": { "username": "bb-account@example.test" }
  },
  "agents": {
    "default": {
      "provider": "copilot",
      "model": "claude-sonnet-5.5",
      "maxSteps": 40,
      "maxModelCalls": 40,
      "context": "Sign in with the credential the goal names. The local app sends no email and has no AI key. Never start a paid run or connect an integration. Not bugs: a link that opens a new tab leaves this one unchanged; accessible text splits around inline links, so judge copy by the rendered screen when a screenshot is available and never report split text alone as broken copy; an infinite-scroll \"Loading more\" sentinel loads when scrolled into view; images lazy-load, so scroll and wait before calling one blank."
    },
    "skeptic": {
      "provider": "copilot",
      "model": "claude-sonnet-5.5",
      "maxSteps": 40,
      "maxModelCalls": 40,
      "context": "<the same context as default>",
      "system": "Distrust every number, date, count, and claim on screen; cross-check each against every other place it appears."
    },
    "fuzzer": {
      "provider": "copilot",
      "model": "claude-sonnet-5.5",
      "maxSteps": 40,
      "maxModelCalls": 40,
      "context": "<the same context as default>",
      "system": "At every input, run the goal's input matrix before anything else, judging each entry before the next. Never take the happy path."
    }
  }
}
```

The target declares no `app.command`, because the app already runs. The
`credentials` hold no password: set `E2E_USER_BB_CART_PASSWORD` and
`E2E_USER_BB_ACCOUNT_PASSWORD` (the seed script's password, 6 or more
characters) in the environment, so the file stays free of secrets. The three
agents are the postures from step 2 as personas: the same model and budgets,
a different stance. An agent inherits nothing from `default` (topic `agent`),
so each repeats the model, budgets, and `context`. Each charter picks one with
`--agent` (step 3). The config has no `tests` or `reporters` key, so repro tests live in the test
project (step 6). Pass `--config e2e.bugbash.config.json` to every
command below, and `open_session {config: "e2e.bugbash.config.json"}` over
MCP.

## 2. Plan charters

A charter is one `e2e explore` goal: one area, one posture, one sentence,
naming the start route and, when needed, the credential (`Sign in as
credential bb-cart. Starting at /cart, ...`). Read the routes, navigation,
and forms first; for a branch, `git diff --stat` against the base.

| Posture | Charter shape |
| --- | --- |
| First-time user | `Starting at /signup, sign up and complete onboarding like a first-time user; report anything confusing, broken, or inconsistent` |
| Numbers and copy | `Starting at /cart, change quantities and apply a coupon; check every price, total, and label against the rest of the page` |
| Edge input | `Starting at /settings/profile, submit each field empty, too long, with unicode and with leading spaces; report validation that is missing or wrong` |
| State | `Starting at /projects, create, rename, and delete a project, reloading and going back after each; report state that is lost or stale` |
| Error paths | `Starting at /login, try a wrong password, an unknown account, and a locked account; report errors that are missing, misleading, or leak detail` |

Aim for five to ten charters, each with its own slug. Overlap is fine;
duplicates merge in step 4. Give each posture its persona (config above),
picked per charter in step 3: a generic agent walks past a stat that
contradicts the same stat on another page, the skeptic catches it. An
edge-input charter names its exact matrix (empty, a 300-character string,
unicode, leading spaces, literal special characters) or it spends the whole
time budget before judging a single result.

## 3. Fan out

One `e2e explore` per charter with its own output directory. The port writes
`report.json` beside the results directory, not inside it, so
`--output .e2e/bugbash/<slug>/results` puts the screenshots and videos in
`results/` and the record in `.e2e/bugbash/<slug>/report.json`. The port has
no `--reporter`, so there is no `summary.md`: the log holds the `Findings`.
`e2e init` gitignores specific `.e2e/` paths, not `.e2e/bugbash/`: add it to
`.gitignore` or delete it when done, and delete it before a new bug bash. `charters.txt` and the logs under it are yours; the rest is the
runner's output, read only.

Run them as background shell jobs, four at a time, not as subagents. One
line per charter, `slug|target|agent|charter`, the agent naming its persona,
then (`xargs -0 -P` is a GNU and BSD extension, present on macOS and Linux):

```bash
mkdir -p .e2e/bugbash
cat > .e2e/bugbash/charters.txt <<'CHARTERS'
cart|web|skeptic|Starting at /cart, change quantities and apply a coupon; check every price, total, and label against the rest of the page
account|web|fuzzer|Starting at /settings/profile, submit each field empty, a 300-character value, unicode, and leading spaces; report validation that is missing or wrong
CHARTERS
while IFS='|' read -r slug target agent charter; do
  [ -n "$slug" ] && printf '%s\0%s\0%s\0%s\0' "$slug" "$target" "$agent" "$charter"
done < .e2e/bugbash/charters.txt | xargs -0 -n 4 -P 4 sh -c \
  'e2e explore "$4" --config e2e.bugbash.config.json --target "$2" --agent "$3" --output ".e2e/bugbash/$1/results" --max-steps 6 --video=on < /dev/null > ".e2e/bugbash/$1.log" 2>&1' _
```

The log's summary prints the findings and the steps. Exit code `1` means
issues were reported (or nothing was explored): read the log either way.
Exit codes `2` and `3` are setup and environment problems; fix them and
rerun that charter alone.

## 4. Merge

Read each log's `Findings` section (topic `explore`); `report.json` has the
same record under `run.explore`. The video is in the attempt's `video/`
directory under `.e2e/bugbash/<slug>/results/`.

Merge findings that describe one defect: same path, same broken behavior.
Keep the clearest reproduction and every charter that hit it. Keep warnings
in a separate list unless the user asked for polish.

A charter's video is not masked: check it for secrets before sharing (topic
`writing-tests`).

## 5. Triage

Sort every finding before writing any test. Read the source to sort them.

| Bucket | Sign | Outcome |
| --- | --- | --- |
| Explorer artifact | A "dead" `target="_blank"` link whose destination opens when clicked with popup capture or navigated to directly, a broken sentence the screenshot renders whole, a "Loading more" sentinel nothing scrolled to, a lazy-loading image or embed | Rejected with the check that settled it; settle this bucket first. A new-tab link whose destination never opens stays a candidate |
| Environment | Fails on a key, a service, or a limit only the local stack lacks (an email provider, an AI key, a billing plan) | Rejected, naming the variable or service; note separately when the app handles the failure badly in a way production users would see, such as showing the raw error |
| Design | The code, its tests, or its copy say the behavior is intended | Rejected, citing where |
| Fixture | The seed data lacks a field real records always have | Rejected, naming the field |
| Candidate | None of the above | Verify it (step 6) |

## 6. Verify

A finding is a model's claim; prove each candidate before reporting it. When
your client can start subagents (Claude Code's Agent or Task tool, for one),
start one per area with its three to five candidates as the log printed
them and these steps, up to four at a time, the `e2e mcp` server's default
session limit. Each reports back, per finding: confirmed or rejected, the
root cause as `file:line` when it may read the source, the repro test path,
and the failure it saw. Without subagents, verify one area after another.

1. Read `actual` against the screenshot, or the video when there is none.
   A finding the evidence contradicts is rejected here. Settle the artifact
   bucket first: a "dead" link's destination must actually open (a valid
   href with a prevented default is still dead), a copy claim must show in
   the rendered screenshot.
2. Write a repro test that follows the reproduction and asserts the
   expected behavior, so it fails today and passes once the bug is fixed.
   Put it in the test project, in a `BugBash` folder with one class per
   area (`BugBash/CartTests.cs`), so the repro tests are easy to find and to
   leave out. Build on the project's fixtures (its test base class, its
   sign-in helper). Prefer `Screen` actions and `Expect.That` with exact
   values; `Agent.ActAsync` for a step that varies, `Agent.AssertAsync` for
   an outcome only judgment can check (topic `writing-tests`). Tag it
   `bugbash`: NUnit `[Category("bugbash")]`, xUnit v3
   `[Trait("TestCategory", "bugbash")]`. Give it its own data
   (`Values.Unique(...)`).
3. Get exact locators from the live app: with the `e2e mcp` server
   registered, `open_session` with the bug-bash config, pass its session id
   to every call, walk the reproduction, and `locate` each locator before
   writing it (topic `mcp`). Close your session when done.
4. Run the class alone: `dotnet test --filter "FullyQualifiedName~BugBash.CartTests"`.
   Confirmed only when it fails with `ASSERTION_FAILED` on the assertion
   that encodes the bug. Any other failure (`LOCATOR_NOT_FOUND`, a timeout,
   a setup error) means the test is wrong: fix it and rerun. A passing test
   means the bug did not reproduce: reject the finding, say so, and delete
   the test (or offer it as a regression test). Only failing repro tests
   stay in `BugBash/`.

## 7. Report

Lead with the confirmed bugs, most severe first. For each: title, path, one
line of expected against actual, root cause when known, steps, screenshot
and video paths, repro test, and the charters that found it. Then the
environment candidates that are production risks, marked unverified. Then
the rejected findings grouped by reason (environment, design, fixture, did
not reproduce), and the warnings. End with the charters run, their cost, and
the areas no charter reached.

The repro tests fail until the bugs are fixed: the gating run leaves them out
(`dotnet test --filter "TestCategory!=bugbash"`) or they stay uncommitted.
Offer to fix each bug: the repro test turning green is the proof, and it
stays as the regression test with its `bugbash` tag removed.

## Rules

- Never report an unverified finding as a bug. "The explorer reported" is
  not "confirmed".
- One charter, one area. A charter that spans the whole app ends at its
  step budget having skimmed everything.
- Seeded data and test accounts the app ships for development are not
  bugs; say so in the agent's `context`.
- Leave the project's files as you found them: the bug-bash config, seed
  script, and repro tests stay untracked until the user asks otherwise, and
  stop the stack you started when the user is done.
