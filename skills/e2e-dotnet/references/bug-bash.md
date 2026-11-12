# Running a bug bash

Upstream's bug bash is many `e2e explore` runs at once, one charter each,
then a verification pass that turns every claimed bug into a repro test that
fails for the reason reported. The port has the one-exploration `e2e explore`
command (topic `explore`) but no fan-out, so run one exploration per charter
by hand.

## Contents

- What still applies
- Rules

## What still applies

The verification half works with plain tests. For each suspected bug:

1. Write one test that reproduces it: open the screen with `App.OpenAsync`,
   reach the state with `Screen` actions or `Agent.ActAsync`, and pin the
   expected behavior with `Expect.That` or `Agent.AssertAsync` (topic
   `writing-tests`). Write it in the shape that topic describes: arrange,
   closed by a wait for the starting state, then the one action that shows
   the defect, then the assertions. The wait keeps a broken setup from
   passing as the defect, and keeps the setup goals cached across the
   failing runs.
2. Run it with `dotnet test --filter "FullyQualifiedName~<Test>"`. It must
   fail, and fail for the reason reported, not for a locator, setup, or
   failed arrange wait problem (topic `debugging`).
3. Run it again. A bug that fails only some of the time is a flake until
   the cause is known.

## Rules

- Report confirmed bugs only, each with its failing test.
- Give each test its own data (`Values.Unique(...)`) and account, so tests
  do not report each other's edits as bugs.
- Never weaken an expectation to make a repro pass or fail.
