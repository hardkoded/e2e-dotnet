---
title: Caching and self-healing
description: How the replay cache records a verified act, heals when the screen changes, and what the trace page shows.
sidebar:
  label: "Caching & self-healing"
---

## Cache mode

`cache.mode` is `off`, `read-only`, or `read-write`. Unset, it is `read-write` locally and `read-only` when `CI` is set. `cache.strict` fails a recording that no longer matches with `REPLAY_STALE` instead of running the step live. Under `cache.strict`, retries replay too, and the run never writes the cache. `cache.dir` resolves against the config file's directory. `E2E_CACHE_DIR` replaces it, so checkouts and worktrees of one project can share a replay cache without editing the config; the mode stays as configured. `E2E_NO_CACHE=1` (or `true`) is upstream's `--no-cache`: for that run the cache is `off` whatever the config says, in CI too and beside a `cache.store`, so every step runs on the live agent and nothing is recorded or evicted. Use it to evaluate a new model, or to run a test once live after it passes. A replay cache of your own (upstream's `cache.store`, an `IStepCache`) replaces the file store: it is code, so set it with `E2EConfig.WithCacheStore(store)`, not in `e2e.config.json`. A store has no directory, so it ignores `E2E_CACHE_DIR`, and CI does not turn an unset mode into `read-only` (the store states its own trust). The file store (`FileStepCache`) takes upstream's `maxBytes` and `writable` as `new FileStepCache(directory, maxBytes, writable)`: an entry over the byte cap (at most 1 MiB, `FileStepCache.MaxCacheWireBytes`) is not written and reads as invalid, and a store that is not writable never writes or deletes. Parallel runs can share the directory: each write replaces its entry file atomically, and two writers on one key leave the last write. On Windows, a write the file system refuses while another write replaces the same entry, or while a reader holds it open, is retried for a moment before the step reports it as not written.

A cache entry belongs to one test, instruction, set of params, and agent. Each of these changes causes a miss: renaming the test or its class, changing the instruction or an ordinary param, running with another configured agent, changing the agent's `context` or a test's `[AgentContext]` (a secret in it enters the key as its name only), or switching to another engine. Changing the model doesn't cause a miss.

A recording follows the app to another port or host that shares its `app.identity`. An agent `navigate` to a full URL on the app is recorded without the origin, as its path, query, and fragment, so the step replays on the origin the app runs on then. Re-record older entries to pick this up.

## Inspect and clear the cache

`e2e cache` ports upstream's `cli/cache.ts`. Each subcommand loads the nearest `e2e.config.json` (or `--config <path>`), reads the directory a run would, and exits 0 on an empty store. It exits 2 on a config or file system error.

```bash
e2e cache ls      # one row per entry: test, target, instruction digest, age, actions
e2e cache stats   # directory, entry count, size
e2e cache clear   # delete every entry
```

| Command | Prints |
| --- | --- |
| `e2e cache ls` | One row per entry (`TEST`, `TARGET`, `INSTRUCTION`, `AGE`, `ACTIONS`): the test and target it was recorded for, the first 12 characters of its instruction's digest, its age, and how many actions a replay would run (`(truncated)` when the recording hit the action cap). Rows are grouped by test, then target, oldest first. |
| `e2e cache stats` | The store directory, the number of readable entries, their total size, and the number of unreadable files when there are any. |
| `e2e cache clear` | Deletes every cache file, then the directory when nothing else is left in it, and reports how many files went. |

| Option | Description |
| --- | --- |
| `--config <path>` | Explicit config path. Default: the nearest `e2e.config.json`. |
| `--cache-dir <dir>` | The store directory, relative to the project root. Default: `E2E_CACHE_DIR`, else `cache.dir`, else `.e2e/cache/`. |

- An entry recorded before entries carried a test, a target, or an instruction prints `-` in that column and still replays.
- A file the store cannot read (truncated, over the 1 MiB ceiling, or from another schema) is counted on stderr, treated as a miss by a run, and removed by `clear`. A crashed writer's temporary file is removed by `clear` too.
- Files the runner never wrote are left alone, and named on stderr.
- `AGE` is the entry file's last write time: the port's entries carry no `createdAt`. The `INSTRUCTION` digest is computed from the instruction the entry stores, where upstream stores the digest.
- A `cache.store` of your own is code, never in `e2e.config.json`, so the command reads files only and has nothing to refuse (upstream exits 2 for a custom store).

## Trace page

With `trace` on for an attempt, the session writes `.e2e/results/<result>/trace.md` when it is released. The default, `retain-on-failure`, keeps the page of every attempt that did not pass. The page is markdown, for you or a coding agent to open. It names the error and the line to look at, then lists every step with its duration, model calls, the hook it ran in, the cache's decision for an agent step, and how the screen changed since the step before. Under a step it tells the node an action landed on, what an assertion read while it waited, where the page went on its own, and what the app logged meanwhile (console output, uncaught errors, failed requests); the web engine collects the app log. It ends with the screen at failure (the location, a screenshot, the screen as the agent reads it, and for a locator that matched nothing, the nodes closest to what it asked for), the video, and what the attempt ran on. A retry is another attempt in the same report. The run writes one `trace.md` for the test from that report, which still names the earlier attempts; the page's steps are the verdict attempt (a flaky test's last failure). Text from the app is escaped, and secrets are redacted. The agent's turns are not recorded yet. A failed locator or matcher also carries its facts on the exception, `E2EException.Details`: the locator, what it expected and observed, how many nodes matched, what it asked for, and how long it waited. See [COMPATIBILITY.md](/compatibility/#trace-page).

## Self-healing tests

A self-healing test keeps passing when the UI changes under a recorded `Agent.ActAsync` step. The replay runs the recording until it no longer matches the screen. Then it hands the rest of the step to the agent, which finishes the goal from the current screen. In `read-write` mode, the same run can save the new flow once a later check passes. Such a step reports `ActResult.Cache.Mode` `agent-concluded`.

Take a step recorded with two actions, and a button that was renamed since:

```csharp
[Test]
public async Task Applies_a_discount_code()
{
    await App.OpenAsync("/checkout");
    await Agent.ActAsync("apply the discount code SUMMER");
    await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("10% off");
}
```

1. The replay types `SUMMER` into the `Discount code` field without a model call.
2. The replay looks for the recorded button `Apply`. The button is now `Redeem`, so no control matches within 15 seconds (`ReplayTimeout`). The replay stops with `target-not-found`.
3. The agent gets the step with a record of what the replay did. It reads the screen, sees `Redeem`, and taps it.
4. The `Expect` passes. At the end of the attempt, the session saves the new recording, with `Redeem` in place of `Apply`.
5. The next run replays the whole step without a model call.

The replay never guesses. Once the replay has performed an action, a recorded control that no longer matches is a hand-off, never a tap on the closest control.

A replay finds a control by its role, name, text, test id, placeholder, input purpose, and the row, list item, or group it sat in. Exactly one control must match. When the app generates the test id of a control (`node-<uuid>`), the control is found again by its other fields, as long as they still identify it. A control with no name is found by its place among the unnamed controls of its kind inside the same named row, list item, or group, and a control known only by a generated test id is found by the id pattern the same way, or when it was the only one with that pattern. The place counts only at the count the recording saw: when a form is still rendering and shows fewer of those controls, the replay looks again (`ReplayTimeout`) instead of picking one. A control with no name that has twins and no named container to tell them apart ends the replay with `target-not-found`; give it a label, or put it in a named row or group. When several controls match and the recording holds no place for them, the replay ends at once with `target-ambiguous`.

### Limits and gaps

A recording is `truncated` when the step took more than 50 actions (possible only with `MaxSteps` above 50), typed a value or URL over 4,096 characters or one that holds a registered secret, or acted on a control with no usable description (no name, text, test id, or placeholder, and no named row or group to tell it from its twins). The entry is still written, as a record of what happened, but a replay never takes it: the step misses with the reason `truncated` and the agent does it live, also under `cache.strict`. Split the step, or pass the secret as a `Secret`.

A `gap` is an action the cache can't repeat: a mutating project tool, or a value the agent read off the screen, typed after it saw a screenshot, or worked out from the date. A replay performs everything before a gap and hands the step to the agent, which does that part live. The recording keeps the rule that flagged a typed value in the `derived` field of its gap (`DerivedReason`): `whole-node` (a control's whole name or text), `minted-token` (a code or id the app showed as its own word), `date`, or `pixels`. Values from the instruction or `params`, and values the agent composed itself, replay. Put stable values in `params`.

The agent's first prompt lists the actions the replay already performed, says why it stopped (`Replay stopped (target-not-found) after 1 of 2 recorded actions.`), and tells it to continue from the current screen without redoing them. When every recorded action ran but the screen does not show the recorded end state (`end-mismatch`), the prompt adds a warning to check whether the step took effect. When an action failed and may have reached the app (`action-uncertain`), the prompt adds a warning to verify the current state before doing anything like it again. The replayed actions count toward the step's action budget and deadline. When the replay stops before it performs any action, whatever the reason (the target is not found or is ambiguous, the recording has a gap, the entry is invalid, the recording started on another screen (`wrong-context`), or the first action failed (`action-failed`) or may have reached the app (`action-uncertain`)), the step is a miss, not a hand-off: outside `cache.strict`, the agent runs it from the beginning with no notice, and under `cache.strict` the step fails with `REPLAY_STALE` (except for `gap`). A custom executor gets the hand-off in `StepExecutorContext.ReplayedPrefix` and must continue from the current screen too.

A healed step records again like a new step: in `read-write` mode, and only when a check after it passes, such as a locator assertion or `Agent.AssertAsync`. Then the recording is saved with the new controls and end state, unless the store rejected the write (`not-written`). Otherwise the old entry goes away:

- The step failed. The next run doesn't replay a flow that broke.
- No check passed after the step.
- The step changed nothing the replay could check, so there is no new flow to save.
- The agent had to act to fix an `end-mismatch`. The next verified run records a clean flow, not the failed flow plus the fix.

When no model answered, the entry stays as it is, unless a failure on the app implicated an unconfirmed act: settlement then evicts it. A cancelled run writes nothing, so the entry stays too. The trace page's `cache:` line says what happened, and `ActResult.Cache` carries the same record (`ReplayedActions`, `TotalActions`, `Reason`, `Detail`).

The agent heals toward the goal in the instruction, not toward the recorded clicks. If the discount button moved, the agent finds it. If discounts stop working, the step or the check after it fails. A goal can still pass in a way you didn't intend, for example with a different discount, so follow each `ActAsync` with a deterministic check of the result, such as the `ToHaveTextAsync("10% off")` above. That check runs on every run, replayed or healed.

With `CI` set and `cache.mode` unset, the cache is `read-only`, so a healed step isn't saved. It hands off again on every CI run and pays for model calls each time. Record it again in a local `read-write` run, then commit `.e2e/cache/`. To fail instead of heal, run CI with `cache.strict`. A step whose recording no longer replays fails with `REPLAY_STALE` and makes no model call. The message ends with how to re-record it: a `read-write` run with `cache.strict` set to false, then commit the changed entry under the cache directory (`.e2e/cache`, `cache.dir`, or `E2E_CACHE_DIR`, relative to the project). A cache directory outside the project is named by its absolute path with no commit to ask for, and a custom `cache.store` is named as the place the run writes the new entry.

## Rerun a test while you fix it

While you fix a failing test, run only that test (`dotnet test --filter "FullyQualifiedName~<Class>.<Method>"`; use `=` with the full name when another test name contains it) and keep the cache `read-write`, the local default, and outside `cache.strict`, which never writes. With `CI` set, an unset mode is `read-only`. A failed run keeps the recordings a check confirmed before the failure, and the next run replays those goals without model calls, as long as each replay still matches the screen.

1. Set `actionTimeout` to `5000` while you iterate, so a wrong locator fails after 5 s instead of 30 s.
2. Don't rename the test or its class, or reword a goal that passes. Each causes a miss: a rename for every goal in the test, a reworded goal for itself.
3. Take NUnit `[Retry]` off the test (xUnit v3 has none). Only the first attempt replays outside `cache.strict`, so a retry runs every goal live.
4. When the test passes, run it one time with `"cache": { "mode": "off" }`. Every goal runs live, which shows the agent still reaches it without a recording.

Each secret reads `E2E_SECRET_<NAME>` first, then the config value. `null` means the variable is required. A test gets one with `Secrets.Get("stripe-key")`. A `credentials` entry (`{ "admin": { "username": "...", "password": "..." } }`) is an account whose password is the secret `admin.password`. `E2E_USER_ADMIN_USERNAME` and `E2E_USER_ADMIN_PASSWORD` win over the config, and a `secrets` entry cannot take the name `admin.password`. A provider, set in code with `E2EConfig.WithSecretProvider` or `WithCredential`, takes the place of a value and is asked on every fill. `Secrets.Get` reads `secrets` entries only; a name it does not know fails with `secret "name" is not configured; add it to config.secrets or set E2E_SECRET_NAME`, and for a credential or its `<name>.password` it adds `; credential "admin"'s password is credentials.user("admin").password`. The account is `Credentials.User("admin")`, and its `Password` is redacted like any secret. Secret values passed to an act never enter cache entries: a name or test id that shows one is stored as `<secret:name>`.

A fixture overrides any value with the matching property, such as `BaseUrl`, `CacheMode`, or `ActionTimeout`, or replaces the whole config by overriding `Config`.
