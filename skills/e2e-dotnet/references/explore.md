# Exploring without a test

## Contents

- What a run does
- Run one
- Budgets
- The verdict
- Reading the result
- Your config applies
- Writing a good goal
- When it does not fit

`Explore.ExploreAsync` runs the agent against the app with a goal instead of
a test: see what it can do with an app before tests exist, hunt for
regressions on a branch, or find what is worth turning into a test. It needs
an `e2e.config.json` with a target and an agent that holds a model, nothing
else. There is no `e2e explore` command yet, so an exploration runs from
code.

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

## Budgets

| Budget | Default | Range | Option |
| --- | --- | --- | --- |
| Exploration steps | 8 | 1 to 12 | `MaxSteps` |
| Wall clock | 10 minutes | 3 to 15 minutes | `Timeout` |
| Actions and model calls per step | 40 each, unless the agent sets `maxSteps` or `maxModelCalls` | 1 to 100 | config |

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
`.e2e/results/<goal>/attempt-1/screenshots/` (`Output` changes the results
directory). Once a secret is filled, no screenshot is kept.

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
saved sessions (`--session`), traces, video, or report file, so an
exploration starts signed out and its record is the outcome only. A whole-app
bug hunt (topic `bug-bash`) still has no fan-out.
