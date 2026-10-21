# Exploring without a test

## Contents

- What a run does
- Run one
- The command
- Budgets
- The verdict
- Reading the result
- Your config applies
- Writing a good goal
- When it does not fit

`e2e explore` and `Explore.ExploreAsync` run the agent against the app with a
goal instead of a test: see what it can do with an app before tests exist,
hunt for regressions on a branch, or find what is worth turning into a test.
It needs an `e2e.config.json` with a target and an agent that holds a model,
nothing else. The command runs from the project directory; the library call
runs from code.

```bash
e2e explore   # goal: "Explore the app and find bugs"
e2e explore "Explore checkout like a first-time buyer and report anything off"
e2e explore --target web --max-steps 4 --headed
```

## What a run does

1. Opens the app on the target's URL.
2. Plans one step: a structured model call (`ExtractAsync`) reads the goal,
   the steps and findings so far, and the current screen, and answers with a
   title and a concrete charter for one flow, or decides the goal is covered.
3. Runs the charter as an `ActAsync` step with the agent's project tools plus
   `report_finding`. The agent reports each defect the moment it has
   evidence: title, `issue` or `warning`, severity 1 to 5, expected, actual,
   reproduction steps. The run adds the path and a redacted screenshot.
4. Repeats until the planner finishes, the step limit, the clock, or three
   failed or blocked steps in a row that reported nothing; then asks for a
   closing assessment.

A failed step does not end the run: it is recorded, and only reported issues
fail the verdict. A run whose charters were all blocked stays blocked. A step
that hits its action or time budget ended at its limit and counts as neither.

## Run one

```csharp
var outcome = await Explore.ExploreAsync(new ExploreOptions
{
    Goal = "Explore checkout like a first-time buyer and report anything off",
    MaxSteps = 4,
    OnProgress = progress => Console.WriteLine(progress),
});

foreach (var finding in outcome.Explore.Findings)
{
    Console.WriteLine($"{finding.Kind} {finding.Severity}: {finding.Title} ({finding.Path})");
}
```

`ExploreAsync` finds the nearest `e2e.config.json` from `Cwd` (or reads
`ConfigPath`), drives a `WebEngine` unless `Engine` names another, and runs
as the config's `default` agent, or the one `Agent` names. An unknown agent
or target, a goal over 2000 characters, a budget out of range, and an agent
with no model throw before anything starts. `Agents` gives agents in code,
by name, over the config's: a model of your own or a project tool is set
there, since `e2e.config.json` names only a provider and a model id.

## The command

| Flag | Default | Effect |
| --- | --- | --- |
| `[goal]` | `Explore the app and find bugs` | One quoted sentence: the area and the posture. |
| `--config <path>` | the nearest `e2e.config.json` | The config file. |
| `--target <id>` | the config's target | The target to explore. |
| `--agent <name>` | `default` | Build the explorer from another configured agent (`agents.<name>`). |
| `--max-steps <n>` | 8 (1 to 12) | Exploration steps at most. |
| `--timeout <ms>` | 600000 (180000 to 900000) | Wall clock; the last minute is for the assessment. |
| `--headed` | off | Show the browser while the agent explores. |
| `--output <dir>` | `.e2e/results` | Where finding screenshots, trace pages, and videos are kept. |
| `--debug` | off | Print the phase timings and the agent step table to stderr. `E2E_DEBUG=1` does the same. |
| `--ai-trace` | off | Record every model call to `ai-trace-<process id>.json` next to the results directory (`.e2e/`), for `npx unbox-ai`. `E2E_AI_TRACE=1` does the same. |
| `--trace [mode]` | the config's `trace`, else `retain-on-failure` | Which attempts keep a trace page, `<output>/<result>/trace.md`. A bare flag is `on`. `E2E_TRACE` sets it too. |
| `--video [mode]` | the config's `video`, else `off` | Which attempts record a video, when the engine can. A bare flag is `on`. `E2E_VIDEO` sets it too. |

A flag out of range exits 2 and names the range. `--session` and `--reporter`
exit 2: the port has no saved sessions or reporters. The modes of `--trace`
and `--video` are `off`, `on`, `retain-on-failure`, `on-first-retry`, and
`on-all-retries`; a value that is not one exits 2. `--trace` and `--video` take
the next word as the mode, so write `--video=on "Explore checkout"` or put the
goal first. An exploration is one attempt, so a retry mode keeps nothing, and a
notice says to pass `--trace on` or `--video on`. The exploration's own verdict, the
issues its findings tell, gets no trace page. Videos mask nothing: they can show
a password the explorer filled. `ExploreOptions` has the same four as `Trace`,
`Video`, `Debug`, and `AiTrace`. `e2e mcp` takes none of them: its sessions
write no trace page.

Every `E2E_USER_<NAME>_USERNAME` and `E2E_USER_<NAME>_PASSWORD` pair in the
environment is an account the explorer can sign in with, named `<name>` in
lower case; a username with no password (or a short one) exits 2.

The terminal shows each step by its title with its duration and findings, and
each finding the moment it is reported as `⚑ high issue  Title (/path)`. At
the end a `Findings` section lists every finding, issues first and the most
severe first, each with where it was seen, expected against actual, the steps
that reach it, and its screenshot path; then the `Assessment` and a summary
with `Findings` and `Steps`. Severity words: critical 5, high 4, medium 3,
low 2, trivial 1. The exit code is the verdict's: 0 when no issue was reported
(warnings do not count), 1 for an issue or nothing explored, 2 for a
configuration or policy error, 3 for an engine, app, or model provider
failure, and 130 when interrupted. `.e2e/report.json` (beside the results
directory, so `--output out/results` gives `out/report.json`) has the record under `run.explore`: the goal, the budgets, why the
run ended, the steps, and the findings. A finding's `artifactId` is left out:
the port names the screenshot by its path, in the terminal and in `ExploreOutcome`.

## Budgets

| Budget | Default | Range | Option |
| --- | --- | --- | --- |
| Exploration steps | 8 | 1 to 12 | `MaxSteps` |
| Wall clock | 10 minutes | 3 to 15 minutes | `Timeout` |
| Actions and model calls per step | 40 each, unless the agent sets `maxSteps` or `maxModelCalls` (a 25 it sets is kept) | 1 to 100 | config |

Start with the defaults. Increase the step or time budget when the result
shows the run stopped before the part of the flow you need. The last minute
is kept for the closing assessment. The replay cache is off for the
exploration, and so are retries.

## The verdict

`ExploreOutcome.Status` and `ExitCode` follow the run's rules:

| Outcome | Status | Exit code |
| --- | --- | --- |
| Steps ran, no `issue` finding, and not every step was blocked | `Passed` | 0 |
| At least one `issue` finding | `Failed` | 1 |
| No step ran and nothing was found | `Blocked` | 1 |
| Every step was blocked, with no `issue` finding | `Blocked` | 1, 2, or 3, from the first blocker |
| An engine, app, or model provider error ended the run | `Failed` | 3 |

An `issue` is a functional defect and fails the run. A `warning` is cosmetic
and does not. `ExploreOutcome.Error` is the `E2EException` that ended the
run, or null for a pass.

## Reading the result

`ExploreOutcome.Explore` is the record: the goal, the budgets, why the run
ended (`Finished`, `StepLimit`, `Time`, `Stuck`, `Aborted`), the closing
assessment in `Summary`, the `Steps` with their status (`Passed`, `Failed`,
`Blocked`, `Exhausted`), and the `Findings`. A step carries `ErrorCode` only
when it did not pass. A finding carries `Step`, `Path`, and `ArtifactId`
only when known. `ArtifactId` is the path, relative to the project root, of
the evidence screenshot, which lands under
`.e2e/results/<result>/attempt-1/screenshots/` (`Output` changes the results
directory). Once a secret is filled, the model gets no screenshot, so none is kept.

`OnProgress` receives each moment as it happens: `Started`, `Planning`,
`StepStarted`, `FindingReported`, `StepFinished`, and `Finished`.

Turn a finding into a test: its `Reproduction` steps are the `ActAsync`
instructions or `Screen` actions, and `Expected` is the assertion.

## Your config applies

The agent's `system` and `context` carry over to the explorer, and so do the
project tools of `AgentOptions.Tools`. A custom `Executor` is replaced by the
built-in agent for the run, with a notice (`Notice`), and needs a model on
the entry or the executor, or the run throws `MODEL_UNAVAILABLE` before it
starts. A project tool may not be named `report_finding`.

`Credentials` gives the accounts the explorer can sign in with, by name (read
one with `Credentials.User`). The planner knows the account names and
usernames, and the agent fills each password with `fill_secret` by the
secret `<name>.password`; the password never reaches a model. Give only the
accounts the exploration needs. Without credentials, the agent signs in with
made-up ones.

## Writing a good goal

Name the flow and the user's goal, for example, "Explore checkout as a
first-time buyer and check that totals match the cart." Include boundaries
when needed, such as "stop before placing the order." Without a goal,
`Explore.DefaultGoal` is used (`Explore the app and find bugs`). A secret
value the goal spells out is redacted before it reaches a title, a file name,
the record, or a model.

## When it does not fit

Findings are the model's claims plus evidence, not verified reproductions:
read `Actual` against the screenshot before filing a bug. The port has no
saved sessions (`--session`), so an
exploration starts signed out. Its record is the outcome, what the command
prints, and `run.explore` in `report.json`. A whole-app
bug hunt (topic `bug-bash`) still has no fan-out.
