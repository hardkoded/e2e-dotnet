# Agent steps

`Agent` is a fixture member like `Screen` and the main way a test drives the
app. Each call is one bounded invocation: a fresh redacted observation, a
deadline, a model-call budget, and no shared transcript. A test with no agent
step makes no model calls and needs no model in the config.

## Contents

- Configure a model
- ActAsync: one goal
- AssertAsync, WaitForAsync, ExtractAsync: one question
- Write instructions the model can execute
- What the model sees
- Budgets and cost
- The replay cache
- Make the agent yours
- Project tools
- Custom executors
- In CI

## Configure a model

Set `provider` and `model` under `agents.default` (topic `setup` lists the
providers and their logins):

```json
{
  "targets": [{ "platform": "web", "app": { "url": "http://127.0.0.1:3000" } }],
  "agents": { "default": { "provider": "copilot", "model": "claude-sonnet-5.5" } }
}
```

- An agents entry takes `provider`, `model`, `judge`, `baseUrl`, `apiKeyEnv`,
  `system`, `context`, `maxSteps`, `maxModelCalls`, `judgmentTimeout`,
  `maxObservationBytes`, `maxInputTokens`, and `providerOptions`. None inherits
  from `default`.
- `maxObservationBytes` (1024 through 16777216, default 262144) is the observation
  payload ceiling for act turns and judgments. `maxInputTokens` (1 through
  1000000, default 64000) is the input one model request may carry, counted in
  UTF-8 bytes. A dense screen is cut to fit under it, with the marker
  `[observation truncated at the resolved observation byte limit]` as its last
  line, and the step's `observationTruncated` is set. The budget is the lower of
  `maxObservationBytes` and `maxInputTokens` less what the rest of the request
  costs (the system text, the context, the ledger, the instruction, and room for
  a screenshot under `Vision`), and never under 1024 bytes. A judgment whose
  request is still over `maxInputTokens` fails with `STEP_BUDGET_EXHAUSTED`
  before it is sent. `AgentOptions`, `E2ESessionOptions`, and the fixture take
  the same two values as `MaxObservationBytes` and `MaxInputTokens`.
- `model` drives `ActAsync`. Judgments use `judge` when set, else `model`.
- No model and no replay that finishes the step is `MODEL_UNAVAILABLE`, and so
  is a model GitHub Copilot does not serve to the account, at the first call.
  Auth failures surface on the first model call as `MODEL_PROVIDER_FAILED`.
- `context` is what the app calls things, sent to every model call, judges
  included. `system` is how the acting agent works; judges never see it.
- In code, `ModelProviders` and `Subscriptions` build the same clients, and a
  fixture can override `CreateModel()` and `CreateJudge()`.
- A custom `IAgentModel` reads each `ModelMessage.Images` of a `user` turn
  (`ModelImage`: `Data`, `MediaType`, `Width`, `Height`). The five built-in
  providers send them after the turn's text. The agent sends none yet.
- A judgment sets `ModelRequest.Schema` (and `SchemaName`) and offers no tools:
  a custom `IAgentModel` must ask its provider for that JSON schema as
  structured output and return the one JSON object as
  `ModelResponse.Content`. The built-in providers send `response_format`
  (chat completions), `text.format` (Responses), or `responseJsonSchema`
  (Google), and make Anthropic and Bedrock call a forced `json` tool.
- The act tool loop sets `ModelRequest.ToolChoice` (a `ModelToolChoice`:
  `Auto`, `Required`, or `Tool(name)`) on every turn: `Required`, and
  `Tool("done")` on the forced conclusion turns. A null choice means `Auto`.
  The built-in providers send
  their own form of it: `tool_choice` (`auto`, `any`, or a named tool) for
  Anthropic, `"auto"`, `"required"`, or a named function for chat completions
  and Responses, `functionCallingConfig` mode `AUTO` or `ANY` (with
  `allowedFunctionNames`) for Google, and `toolConfig.toolChoice` for Bedrock.
  Anthropic and Bedrock refuse a forced tool while the model thinks, so they
  send `auto` and return a `ModelWarning` (`Type` `unsupported`, `Feature`
  `toolChoice`) in `ModelResponse.Warnings`. A custom `IAgentModel` may ignore
  the choice, since the loop offers only `done` on a forced turn either way.
  When a model answers a forced choice with an HTTP 4xx naming `tool_choice`
  (for example Claude Fable 5.1's `tool_choice: type "tool" and "any" are not
  supported for this model`), answers a forced turn in prose, or returns the
  `toolChoice` warning above, the loop sends the request again (or continues
  the step) with `Auto` under the same instructions, and starts later steps on
  that model in `Auto` (a built-in provider model is remembered by its class and model id for the process; any other model by object). A custom `IAgentModel` that fails a forced
  choice should fail with a message that says so, so the loop can read it.
- A custom `IAgentModel` reports each call's tokens, cache tokens, and cost in
  `ModelResponse.Usage` (a `ModelCallUsage`; it was `ModelUsage`, which is now
  the step's internal accumulator, as upstream's). The step's report sums
  them: a count that is negative is dropped, a sum past 2^53-1 is capped, and
  a cost total that overflows is omitted. A call with no `Usage`, or with one
  side of the token counts missing, marks the step's `tokenAccounting` as
  `adapter-upper-bound`; `peakTokensPerCall` is the largest call.

### Choose an agent

Tests use `agents.default`. Pin another to a test or a class, or name one on
a call. The closest setting wins: the call, then the method, then the class
(a nested class after its enclosing class), then the run.

```csharp
[Agent("buyer")]
public class CheckoutTests : E2ETest
{
    [Test]
    public async Task PaysForOneItem() { /* acts as buyer */ }

    [Test]
    public async Task ReordersAndTheAdminApproves()
    {
        await Agent.ActAsync("reorder the most recent order");
        await Agent.ActAsync("approve the pending reorder", new ActOptions { Agent = "admin" });
    }
}
```

`E2E_AGENT=thorough dotnet test` sets the run's agent for tests that pin none.
It takes one name; a pin is never replaced. An unknown name, on a pin, a call,
or `E2E_AGENT`, is `INVALID_ARGUMENT`, naming the configured agents and the nearest one (`did you mean "ux"?`). There is no list of agents that runs
one test once per persona. Each agent records its own replay entries, keyed by
its name and context.

`[AgentContext("The seeded workspace is called \"Acme Trial\".")]` on a class
or a method adds facts only that test knows. It joins the agent's `context`,
outer layer first, one per line, and reaches the judges too, so keep
instructions out of it. A changed text records again.

Under a test, every prompt of the built-in agent and the judges also opens with the test's title, such as
`Test: billing > upgrades the tier`. Under NUnit or xUnit the title is the test's
full name (`CacheTitle`), as `Test: Billing.UpgradeTests.Upgrades_the_tier`; under
`e2e explore` it is the goal. An `e2e mcp` session runs no test and has no such
line. The judges read it too, so name a test for what it covers, not for the
verdict you expect. The title goes in the step's prompt, not the system prompt, so provider prompt caching still serves every test of a run, and the context digest of the replay key leaves it out (the key still carries the test id, so a renamed test records again). A custom executor builds its own prompt: it reads the title from `context.Attempt.TitlePath` if it needs it.

## ActAsync: one goal

```csharp
await Agent.ActAsync("add a todo named \"Buy milk\"");
await Expect.That(Screen.GetByRole("listitem", "Buy milk")).ToBeVisibleAsync();
await Agent.ActAsync("mark the \"Buy milk\" todo as done");
await Expect.That(Screen.GetByRole("checkbox", "Buy milk")).ToBeCheckedAsync();

var member = Credentials.User("member");
await Agent.ActAsync("sign in with the given credentials", new ActOptions
{
    Params = new Dictionary<string, object?>
    {
        ["username"] = member.Username,
        ["password"] = member.Password, // a Secret: the model sees its name, the runner fills the field
    },
});
```

`ActAsync(instruction, options?)` runs a multi-action flow to a verdict. A
pass returns an `ActResult` with `Summary`, `ModelCalls`, `Actions`, and
`Cache` (the replay cache's part). A failed or blocked verdict throws an
`AgentException` whose `Message` reads `agent.act failed: <summary>` or
`agent.act blocked: <summary>` (`agent.assert` for an assert; a hard stop such as `STEP_TIMEOUT` or
`STEP_BUDGET_EXHAUSTED` that ends the step with no verdict, or over a passed or code-less one, has no prefix) and whose `Code` says why: `ACTION_FAILED` (product failure);
`STEP_BUDGET_EXHAUSTED`, `STEP_TIMEOUT`, `CONTEXT_OVERFLOW` (out of room);
`AUTH_CREDENTIAL_UNAVAILABLE`, `AUTH_CREDENTIAL_INVALID`,
`SECRET_UNAVAILABLE`, `ENVIRONMENT_UNAVAILABLE`, `SEED_DATA_MISSING`,
`TEST_SETUP_FAILED`, `AUTOMATION_UNSUPPORTED`, `POLICY_DENIED` (blocked from
outside, `Blocked` is true); `STEP_NO_CONCLUSION` (the turns ran out without a
`done` call); `MODEL_OUTPUT_INVALID` (unusable answer),
`MODEL_REFUSED` (the model refused the prompt). Full list: topic `debugging`.

`ActOptions`:

- `Params`: values the instruction names. The runner fills a `Secret`, at any
  depth of the params, and a `Values.Unique(...)` value keeps the cache
  working across runs. The substitution also covers the value as a URL spells
  it (`Ada%20Lovelace` in a path, `Ada+Lovelace` in a query), as the slug an
  app derives for a record path (`ada-lovelace`), and a multi-line value as
  the screen shows it, on one line. A short value is never matched inside the slot written for a longer one. The recording holds each as a `{{param:/name}}` placeholder (`{{param:/name|uri}}` for an encoded spelling); a recording with a placeholder the call cannot fill (a param the call did not mark, an unknown encoding, or a value that encoding cannot spell) is a miss, `invalid-entry`. A `Values.Unique` value cannot contain `{{param:`. A `Values.Unique` value may sit at any depth of `Params` (`/owner/tags/1`). The slot also stands for the value in a target's name, in an upload path and in an end anchor (a status reading `Saved E2E-abc` is recorded as its slot and checked with this run's value). A step that typed, read or navigated to text that itself spells `{{param:` is not recorded, so it runs live each time. A step is not recorded either when a `Values.Unique` value is spelled by another param (at any depth, as text, a number, or a flag, or in a URL spelling of it), since the recording could not tell which param it came from.
- `Timeout`: 30 s by default (`StepTimeout` on the fixture).
- `MaxSteps` (default 25 actions), `MaxModelCalls` (default 25): may only
  lower the agent's limits, else `INVALID_ARGUMENT`.
- `Agent`: the named agent that runs the step.
- `OnAction`: an `ActionHook` (`Task` from an `ActionEvent`) that runs after
  each action the step completes, once the screen settles, and before the next
  action. That includes actions the replay cache replays. The step waits for
  it, and its time counts toward `Timeout`. Use it to observe, never to drive
  the app. If it throws, the step fails with a message that starts with
  `agent.act onAction failed`. A check inside it never confirms the step's
  recording. The event has `Index` (1-based, the action's slot in the step's
  budget), `Action` (`tap`, `navigate`, and so on; a mutating project tool
  reads `tool:<name>`), `Summary` (what the action did, secrets redacted), and
  `Source` (`ActionSource.Model`, or `ActionSource.Replay` for a cached action).
  Match on the event to read an action's details: `TapAtActionEvent.Point` and
  `HoverAtActionEvent.Point` for `tapAt` and `hoverAt`, `DragAtActionEvent.From`
  and `To` for `dragAt` (`using E2E.Engine;` for `Point`). Points are in the
  same space as `Screen.TapAtAsync` and `BoundingBox`, rounded and clamped to
  the viewport. A replay reports the points it dispatches on the current
  screen. Mobile engines have no `hoverAt` or `dragAt`. `ActionName` is the
  action as an enum (`TapAt`, `Navigate`, ..., `Tool`), and `ToolName` names a
  mutating project tool (`Action` still reads `tool:<name>`).
- `OnBeforeAction`: a `BeforeActionHook` (`Task` from a `BeforeActionEvent`)
  that runs before each action, replayed ones included, and the step waits for
  it. Use it to capture the screen an action started on: the event has the
  same `Index` the action's `OnAction` event gets (1-based, restarting in each
  act call) and `Source`. It does not wait for the screen to settle. A
  mutating project tool runs it too, a read-only tool does not. If it throws,
  the step fails before the action runs, with a message that starts with
  `agent.act onBeforeAction failed`, and the step's recording is left alone. A
  check inside it never confirms the step's recording. An action can still
  fail after it, with no `OnAction` call.

```csharp
await Agent.ActAsync("open settings and turn on dark mode", new ActOptions
{
    OnAction = e =>
    {
        Console.WriteLine($"{e.Index} {e.Action}: {e.Summary}");
        return Task.CompletedTask;
    },
    OnBeforeAction = e => App.ScreenshotAsync($"before {e.Index}"),
});
```

The tools the model can call: `observe`, `tap`, `double_tap`, `long_press`,
`right_click`, `hover`, `drag`, `fill`,
`fill_secret` (a `Secret` from `Params`, by name), `press`, `select`,
`check`, `uncheck`, `clear`, `upload`, `scroll`, `scroll_to`, `navigate`, `back`,
`resize_viewport`, and `done`. `scroll` and `scroll_to` come with an engine that
can scroll, `back` with one that has history, `resize_viewport` with one that
declares `EngineCapabilities.Resize` (the web engine), and `upload` with one that declares
`EngineCapabilities.Upload` (the web engine). `long_press`, `right_click`,
`hover`, and `drag` come with an engine whose `IEngineSession.ActionKinds`
names `longPress`, `secondaryTap`, `hover`, and `dragTo`: the web engine lists
all four, the device engines all but `secondaryTap`, and the document engine
none. `drag` takes a `target` and a `to`, the node to drop on, each named like
any node (`ref`, or `role` and `name`); both are found on one look at the
screen. A replay finds both ends again before it drags and hands the step to
the model when either is gone. `resize_viewport {width, height}` sets the
viewport size in CSS pixels, 200 to 4096 per side, to check a layout at phone,
tablet, or desktop width. The size holds until the next resize, and the
result shows the screen at the new size; take a `screenshot` to check the
layout itself (overflow, overlap, clipped or hidden content). The trace cache
records it as `resize` and replays it as given, and a leading resize does not
count against the navigate opener of a recording. `dismiss_keyboard` (no input) comes with an engine that can hide its
keyboard (the mobile engine): it hides the on-screen keyboard when it covers what
the model needs to reach. `screenshot` and the point tools `tap_at`,
`type_at`, `press_at`, and `select_at` are always offered (see "Screenshots in
`ActAsync`"), and `hover_at` and `drag_at` with an engine that takes a bare
point for a hover or a drag (the web engine does) or that lists `hover` or
`dragTo` in its `ActionKinds`. `press` takes `times`, 1 to 20 (default 1): the
first press goes to the node and the rest to whatever has focus, one recorded
action each, so `Shift+ArrowLeft` with `times` of 8 selects a word and `Tab`
with `times` of 2 walks focus on. A step that needs a verb its engine lacks is
`AUTOMATION_UNSUPPORTED`. Do that part with `Screen` actions instead.
`upload` takes a target and `files`, project-relative paths such as
`fixtures/photo.png`, and attaches them to a file input or to a control that
opens a file chooser. The harness authorizes the paths before the engine sees
them: a path outside the project root (through a symlink too), a hidden file
or one under a hidden directory (`.env`, `.git`, `.e2e`) is refused as
`POLICY_DENIED`; a file that does not exist, a directory, an empty list, or
more than 16 files is `INVALID_ARGUMENT`. A refused upload costs no action
slot. The allow and each `POLICY_DENIED` refusal are `policy` events named
`upload.path` on the step. The paths are recorded as given and authorized
again on replay.
On an engine that declares `EngineCapabilities.TapModifiers` (the web engine),
`tap`, `double_tap`, and `right_click` take an optional `modifiers` list (`Shift`, `Control`,
`Alt`, `Meta`, `ControlOrMeta`) to hold keys for the click, as a Shift-click
extends a selection. The held keys are recorded and replayed from the cache.
A project tool adds its own name to this list (see "Project tools").
`fill_secret` fills any editable field, a rich-text editor included, with a
generic secret; a password secret needs a password field.

## AssertAsync, WaitForAsync, ExtractAsync: one question

```csharp
await Agent.AssertAsync("the dashboard shows a trial badge"); // one look, one judgment

await Agent.WaitForAsync("the export finished and a download link appeared", new WaitForOptions
{
    Interval = TimeSpan.FromMilliseconds(500),
    Timeout = TimeSpan.FromMinutes(2),
});

var data = await Agent.ExtractAsync<TodoSummary>("every todo title and how many remain");
Assert.That(data.Titles, Does.Contain("Buy milk"));

public sealed record TodoSummary(IReadOnlyList<string> Titles, int Remaining);
```

- `AssertAsync` does not poll. False is `ASSERTION_FAILED` with the model's
  explanation. Too little on screen to decide is `ASSERTION_INCONCLUSIVE`,
  also a failure, so reach the right screen first and ask about what is
  visible.
- Judgments see the statement, its `Params`, the agent's context, the test
  title, and the current screen, never prior steps. The judge answers `holds`, `fails`, or `inconclusive` in one
  `agent-judgment-2` JSON object (`protocolVersion`, `explanation`, `verdict`;
  `schema/agent-judgment-v2.schema.json`). `fails` is `ASSERTION_FAILED`,
  `inconclusive` is `ASSERTION_INCONCLUSIVE`. An answer outside that grammar
  gets one repair round, then `MODEL_OUTPUT_INVALID`.
- `AssertAsync` and `WaitForAsync` take `Params` like `ActAsync`, with the
  same rules: `INVALID_ARGUMENT` when invalid, a `Values.Unique` value read as
  its string, and the statement refers to a value as `{name}`. The judge sees a
  `Secret` by name only, never its value, so check a secret's value on screen
  with a locator assertion. A custom executor gets an assert's params in
  `Step.Params`, and can name a secret but never fill it.

  ```csharp
  var name = Values.Unique($"acme-{DateTime.UtcNow.Ticks}");
  await Agent.ActAsync("create a company named {name}", new ActOptions { Params = new Dictionary<string, object?> { ["name"] = name } });
  await Agent.AssertAsync("the table shows company {name}", new AssertOptions { Params = new Dictionary<string, object?> { ["name"] = name } });
  ```
- A value shown in more than one place (a total in the summary and on the
  pay button) must agree everywhere, or the judgment fails. Name the one you
  mean (`"the order summary total is $42.00"`) when only it matters.
- `WaitForAsync` judges at once, then every `Interval` (default 3 s) only
  when the screen changed, and is `STEP_TIMEOUT` after `Timeout` (default the
  agent's `judgmentTimeout`, 30 s). A screen capture that outlives `Timeout`
  is `STEP_TIMEOUT` with `agent.waitFor exceeded its N ms timeout`; once a
  judgment has been made, the timeout reads `waitFor timed out; last
  judgment: ...`.
- `ExtractAsync<T>`: the type is the schema. The model sees the JSON schema
  of `T`, and the answer must deserialize into `T`. Data the screen does not
  show is `ASSERTION_INCONCLUSIVE`; to accept absence, make the member
  nullable and ask for it (`"the phone, or null when none is shown"`).
- `Vision` says what the judge is shown: `VisionMode.Off` (the default) is the
  text snapshot, `On` adds a masked screenshot, and `Only` sends the screenshot
  and no snapshot. Use `On` when the answer is in pixels, such as a chart, a
  canvas, or an overlay. Use `Only` when the snapshot would answer the wrong
  question, such as whether an overlay covers a form the snapshot lists as
  present. All three take it: `AssertOptions.Vision`, `WaitForOptions.Vision`,
  `ExtractOptions.Vision`.
- A `Secret` fill does not turn the screenshot off: pixels still reach the
  model, with secure fields masked and the value kept out of the text. A
  generic secret in a plain input, or a value the app shows outside a secure
  field, is in the image and reaches the model provider, so use test
  credentials. `Only` fails with `POLICY_DENIED` rather than answer from the
  snapshot when the engine captures no pixels or masks fewer regions than the
  snapshot has secure fields.
- An inconclusive judgment made without pixels ends with a hint to pass
  `Vision = VisionMode.On`, unless pixels cannot help (the engine captures
  none).
- `AssertAsync` keeps a redacted screenshot of the screen it judged under
  `.e2e/results/<result>/attempt-<n>/screenshots/`, and
  `AgentException.Screenshot` names it on a failure. Set
  `AssertOptions.Screenshot = false` to keep none. After a secret fill it is
  still kept: it masks secure fields only, so a secret the app shows anywhere
  else is in the image.

## Write instructions the model can execute

- One goal per `ActAsync`. The test sets the order of goals; the model finds
  the path.
- Keep each goal atomic: one outcome that one check confirms. When two
  parts of an instruction each leave a state worth checking, they are two
  goals: give each its own `ActAsync` and check. Steps toward a single
  outcome are the path, not more goals: scrolling to a control, closing a
  banner, opening the menu that holds it, filling the fields of one form.
- A split flow fails at the goal that broke. Each goal replays from its own
  recording and gets its own `MaxSteps` and `MaxModelCalls`; the test
  timeout still caps them all.

```csharp
// BAD: three goals in one instruction, one recording, no check between them.
await Agent.ActAsync("open the Pro plan, set the seats to {seats}, and check out", new ActOptions
{
    Params = new Dictionary<string, object?> { ["seats"] = 3 },
});

// GOOD: three atomic goals, each pinned before the next.
await Agent.ActAsync("open the Pro plan");
await Expect.That(Screen.GetByRole("heading", "Pro plan")).ToBeVisibleAsync();
await Agent.ActAsync("set the seats to {seats}", new ActOptions
{
    Params = new Dictionary<string, object?> { ["seats"] = 3 },
});
await Expect.That(Screen.GetByRole("status", "Seats")).ToHaveTextAsync("3 seats");
await Agent.ActAsync("check out");
await Expect.That(Screen.GetByText("Order confirmed")).ToBeVisibleAsync();
```

- Use the words on screen: `"open the Billing tab"`, not `"upgrade"`.
- Values go in `Params`, never pasted into the instruction.
- Do not describe mechanics the runner handles: waiting, scrolling, retries.
- Pin every act's outcome right after it; that check lets the cache record:

```csharp
await Agent.ActAsync("create a workspace named \"Atlas\" on the Pro plan");
await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("Created \"Atlas\" on the Pro plan");
```

Off-screen state (a database row) can land after `ActAsync` returns; use
`Expect.Poll` on the read instead of a sleep.

## What the model sees

A redacted text snapshot of the screen (roles, names, text, states), and
your `context`. Never raw HTML, cookies, headers, environment values, or a
`Secret`'s value; password fields are masked. Each model turn gets the whole
screen. A line lists a node's `testid`, a link's `href`, and the
`placeholder` of a field with no name, so the model can tell twins apart. An
`href` on the app's origin shows as its path, any other as the whole URL.
The page reduces it first to origin and path, with `?…` for a dropped query,
`#…` for a fragment, and `data:…` or `javascript:…` for an inline payload,
so a token in a link never reaches the model. A target over 256 characters
is redacted, then cut and ends with `…`. `GetAttributeAsync("href")` still
reads the whole value. Nothing the model returns runs as code or selectors: the runner
validates every tool call first.

The text selected inside the focused field is listed as `selection="..."`
after its value, when the engine reports one (the web engine does for an
input, a textarea, and a `contenteditable` editor). A secure field has
none, and neither does a field whose value or text holds a registered
secret, so selected characters of a secret never reach the model. A
`press` of `Shift+ArrowLeft` shows what it selected.

### Screenshots in `ActAsync`

The model starts a step from the text snapshot. Its `screenshot` tool returns
a masked screenshot of the screen (an image on the next turn, with its pixel
size and its scale to CSS pixels in the result). Once it has taken one, every
later action in the step answers with a fresh screenshot, so the model sees
what the action did. A screen with no control to act on (a canvas) opens with
a screenshot. An engine that captures no pixels, or masks fewer regions than
the screen has secure fields, gets none, and the result says so; the model
works from the text. A superseded screen past 32 KB, and all but the newest
two screenshots (in batches of three), are elided from the request. The step
report carries `visionInput` and `pixelBytes`. When an engine cannot read the
semantic tree (`Observation.TreeUnavailable`, answered to a look with
`ObserveOptions.PixelFallback`), the step works from the screenshot alone:
earlier node references are refused and the step is not cached. If no masked
screenshot can replace the tree, the step ends `POLICY_DENIED`. `ActOptions`
has no `Vision` option.

The point tools act at a point of the latest screenshot, given in that image's
pixels (x from the left edge, y from the top). They are for a target the
screen does not list; the model is told to prefer ids. Before a screenshot
they cost nothing and answer with the line that says to take one.

- `tap_at`, `hover_at`, and `drag_at` (`x`, `y`, and for a drag `to_x` and
  `to_y`) send the bare point to the engine, scaled into CSS pixels and
  clamped to the viewport, when it declares the pointer kind. The result names
  the control the point landed inside, or says no listed control is there. An
  engine without the bare point acts on the enabled control that contains the
  point by its id instead (`tap`, and `hover` where the engine lists it), and a
  point on nothing listed fails with `UNSUPPORTED_CAPABILITY`. `drag_at` on
  such an engine joins two points that land on two different listed controls by
  id, as `drag` does; a drag inside one control, or from a point on nothing
  listed, fails the same way.
- `type_at`, `press_at`, and `select_at` act by id on the enabled control the
  point lands on (`value` or `key` beside the point). A point on nothing listed
  is an action failure the model can read, and costs no action, except with a
  keyboard (the web and document engines): `type_at` and `press_at` then tap
  the point to focus it and go through the keyboard. `type_at` takes `replace`
  to clear the field first. A listed node the engine cannot fill (a canvas, a
  custom widget) takes `fill` through the keyboard too, after a tap that
  focuses it, and the result says so. `fill` with no target types into the
  focused field. `select_at` always needs a listed control.

Over MCP, the session's `screenshot` is the one the point tools read their
points from, and the point tools follow it in the catalog.

A custom executor reaches the same verbs as `Actions.TapAtAsync`,
`HoverAtAsync`, `DragAtAsync`, and `HitTestAsync`, and, when the engine
declares `Keyboard`, `TypeTextAsync` and `PressKeyAsync` for the focused field
(`ExecutorVerb.TypeText` and `PressKey`), and, on an engine that can hide its
keyboard, `DismissKeyboardAsync` (`ExecutorVerb.DismissKeyboard`). On an
engine that declares `EngineCapabilities.Resize`, `ResizeAsync(new TraceViewport(width, height))`
(`ExecutorVerb.Resize`) sets the viewport, 200 to 4096 on each side, for this
page and every page the attempt opens after it. The trace
records them as `typeText`, `pressKey`, `dismissKeyboard`, and `resize`. The trace cache records a
point with the viewport size and where the point sat inside the node under
it. A replay re-finds that node and taps the same place in its live box; a
viewport of another size hands the step over (`viewport-changed`).

The loop watches for a run of failed calls too, of any tools and with any
inputs: three in a row add a notice that asks the model to change approach,
and five in a row leave it only `done`. A call that worked starts the count
over. A turn with no tool call counts as a failure and is answered with a
notice that asks for a tool call or `done`.

## Budgets and cost

| Call | Model calls | Default timeout |
| --- | ---: | --- |
| `ActAsync` | up to `maxModelCalls` (25) | 30 s |
| `AssertAsync` | 2 | 30 s |
| `ExtractAsync` | 2 | 30 s |
| `WaitForAsync` | up to `maxModelCalls` (25) | 30 s |

Slow model calls: raise `Timeout` on the call or the fixture's
`StepTimeout` for acts, `judgmentTimeout` for judgments, and `actionTimeout`
for slow UI. `STEP_TIMEOUT` and `STEP_BUDGET_EXHAUSTED` fail the test;
smaller goals help.

A model request the provider refuses as too large ends a judgment with
`CONTEXT_OVERFLOW`. `ActAsync` shrinks its history once first: it drops every
superseded screen, cuts any text past 16,384 characters to its head with a
notice, and sends the request again. The refused request spends no model
call. A second refusal, or a first one with nothing to shrink, is
`CONTEXT_OVERFLOW`.

## The replay cache

A passing `ActAsync` saves its actions once a later check verifies the
outcome. The next run replays them with no model call, and the live agent
continues when the app no longer matches. `AssertAsync`, `WaitForAsync`, and
`ExtractAsync` always run live.

- On by default (`read-write`), `read-only` in CI, off with
  `"cache": { "mode": "off" }`. Entries live in `.e2e/cache/`; deleting the
  directory only slows the next run.
- An entry is written only after a later locator expectation,
  `AssertAsync`, `WaitForAsync`, or `locator.WaitForAsync` passes, so an
  unchecked act never replays. Locator reads and `ExtractAsync` verify
  nothing.
- A replay re-finds each control by role, name, test id, placeholder, input
  purpose, and its named row (an unnamed control with twins and no named
  container hands off with `target-not-found`, and so does one known only by
  a generated test id once that id is gone), and needs the recorded start
  route unless the recording opens with a navigation. A test id the app
  generates is forgiven when the other fields still identify the control.
  Twins are told apart by the place the recording noted, at the same count
  only; a form still rendering shows fewer of them, so the replay looks
  again before it hands off with `target-ambiguous`. A location on the app's own origin (the `App.Url`'s) is
  compared by its path, query, and fragment, so a recording follows the app
  to another port or host; a screen on any other origin is another route.
  An agent `navigate` to a full URL on the app is recorded as its path, query,
  and fragment, so the step replays where the app runs then; re-record older
  entries to pick this up.
  It passes alone only when the recorded end route is back,
  every control that appeared (with its text, value, and checked or
  selected state) is there, every one that went away is gone, at least one
  of those changed during the replay, and no new alert showed; otherwise
  the agent takes over mid-step. A replay waits for a slow effect for as
  long as the recording needed (a report that takes half a minute), up to
  what the step's clock leaves. A scroll on a list that filled at least
  half the screen scrolls the viewport instead when the list can no longer
  be re-found (web engine only); a smaller region hands off. The agent's
  live scroll and `scroll_to` do the same: a list the tree renumbered is
  re-found by its place (the same role, mostly the same box), one that filled
  the screen and is gone scrolls the viewport, and a smaller one that is
  gone fails with `LOCATOR_NOT_FOUND`. `scroll_to` with a text reads each
  screen once when it has visibly scrolled, and reads again until it holds
  still otherwise. A single changing time value (`12:05`) beside other text
  does not count as the list moving. The web engine does not observe Next.js
  App Router's route announcer, so its title echo never counts. Text that
  reads differently on every run, a date, an ISO 8601 instant, a time, an
  id, is not checked. In an alert it reads as a placeholder, so a replay
  that shows `Saved at 2026-10-24T15:30:00Z` for a recorded
  `Saved at 2026-10-24T15:41:27.955Z` passes, whatever the precision or
  offset of the instant.
  An entry's key holds the test and its class, the instruction and params,
  the agent, and its `context`, so renaming the test or its class records
  every goal in it again, and rewording a goal records that goal again.
  `ActResult.Cache.Reason` says why a replay missed:
  `no-entry`, `invalid-entry`, `wrong-context`, `target-not-found`,
  `target-ambiguous`, `action-failed`, `action-uncertain` (the replay stopped
  before any action; after one it hands off; under `cache.strict` a miss other
  than `no-entry`, `gap`, `truncated`, and an unreadable store fails with `REPLAY_STALE`), `end-mismatch`, `gap` (the recording stops at a
  project tool, see below), `truncated` (the recording hit a limit and never
  replays: more than 50 actions, a typed value or URL over 4,096 characters or
  one that holds a registered secret, or a control with no usable description;
  split the step, or pass the secret as a `Secret`).
  `ActResult.Cache.Detail` says where the replay stopped and why
  (`at action 2 of 3, tap button "Apply": ...`). After an `end-mismatch` it
  names what did not hold: the recorded nodes that did not show, the ones that did not go away, and
  any alert the recording never saw, or says the outcome already showed
  before the replay acted.
- A replay finishes as soon as the recorded changes show, which can be before
  content below them has loaded. To check content that loads after the step,
  use `WaitForAsync`, which waits for it, rather than `AssertAsync`, which
  judges the screen once.
- A step recording no actions, or changing nothing on screen or in the
  route, creates no entry. The screen it passed on is read once it holds
  still, so a late render still counts. One whose `Values.Unique` value
  equals another param's value is not recorded either
  (`ActResult.Cache.NotRecorded`: `param-collision`).
- An entry records the agent's configured model (`IAgentModel.Name`, provider
  and model id together) as `recordedBy`. A step that replays it reports it as
  `ActResult.Cache.RecordedBy`, null for an older entry or an agent with no
  configured model. Changing the model still hits the entry. To judge a new
  model, record again without the cache.
- The trace page tells what became of a step's recording once the attempt
  ended: `recording saved`, `recording kept`, `recording not saved: no check
  passed after this step` (follow the step with an assertion), `recording
  deleted ...` (its replay ended in a failure or a repair, or no check
  confirmed it), `nothing recorded: the step changed nothing a replay could
  check`, or `recording not saved: the cache store did not write it`. It also
  says `recorded with <model>` for a replayed step. `recording deleted` means
  the store deleted the entry. A custom `IStepCache` whose `Delete` throws
  keeps the entry: a replayed or re-recorded step then reads `recording not
  saved: no check passed after this step` (the text does not name the failed
  delete, as upstream), and a failed step's replay gets no line.
- While you fix a test, keep the cache on: each rerun replays the goals whose
  check passed instead of calling the model for them (SKILL.md, "Rerunning a
  test while you fix it"). `"cache": { "mode": "off" }` is for ruling out a
  stale replay (topic `debugging`, "Tools"), and for one live run once the
  test passes.
- Only the first attempt replays, except under `cache.strict`, where a retry
  replays too. Otherwise a `[Retry]` attempt runs live and still records.
- Commit `.e2e/cache/` to share replays with CI and teammates.
- `cache.strict` fails a recording that no longer replays with
  `REPLAY_STALE`, also when the recording sits under an old key (topic
  `running`). The message says how to re-record: a `read-write` run with
  `cache.strict` set to false, then commit the changed entry under the cache
  directory. A version that
  changed the cache key (the one with the `trace-1` key) invalidated every
  earlier recording: re-record them all once.

## Make the agent yours

1. **The goal.** A failed step usually named something the screen does not
   show; reword it with on-screen labels.
2. **`context`.** Vocabulary every step needs (plan names, what a
   "workspace" is), once on `agents.<name>.context` (at most 16 KiB).
3. **`system`.** How carefully the agent verifies and what it never does.
   A careful QA persona and a fast smoke agent are two `system` prompts on
   one model.
4. **Tools.** A test API the agent may call mid-flow (seed a cart, mint a
   coupon), as a project tool (below).
5. **The model and its options.** `providerOptions` for reasoning effort
   (wire names, such as `reasoning_effort`), or another model for one
   agent.

## Project tools

A project tool is a function the model can call during a goal, such as a
request to your test API or a data setup step. Build an `AgentTool` and give
it to the default agent in a fixture, keyed by the name the model calls it
by:

```csharp
using System.Text.Json.Nodes;
using E2E;
using E2E.NUnit;

public sealed class CheckoutTests : E2ETest
{
    private static readonly HttpClient Http = new();

    protected override IReadOnlyDictionary<string, AgentTool>? AgentTools { get; } =
        new Dictionary<string, AgentTool>
        {
            ["seed_cart"] = new AgentTool(
                description: "Seed the cart with a SKU via the store test API",
                inputSchema: JsonNode.Parse("""
                    { "type": "object", "properties": { "sku": { "type": "string" } }, "required": ["sku"] }
                    """)!.AsObject(),
                mutates: true,
                executeAsync: async (input, token) =>
                    await Http.GetStringAsync($"http://localhost:3000/api/test/cart?sku={Uri.EscapeDataString((string)input["sku"]!)}", token)),
        };

    [Test]
    public async Task Member_checks_out()
    {
        await Agent.ActAsync("seed the cart with SKU {sku}, then check out", new ActOptions
        {
            Params = new Dictionary<string, object?> { ["sku"] = "SHIRT-BLUE-M" },
        });
        await Expect.That(Screen.GetByText("Order confirmed")).ToBeVisibleAsync();
    }
}
```

Outside a fixture, `AgentOptions.Tools` and `E2ESession.AgentTools` take the
same dictionary. A config file cannot declare one, since a tool is code.

- `mutates: true` marks a tool that changes state. The call takes a slot of
  the step's action budget, and the replay cache cannot replay it: the
  recording stops at the tool (`ActResult.Cache.Reason` `gap`), a replay runs
  the actions before it, and the agent runs the rest live. Under `cache.strict`
  that is not `REPLAY_STALE`: a recording that stops at a project tool is a
  step the cache cannot finish. The recording keeps the tool's name, never its
  arguments.
- `platforms: ["ios", "android"]` offers the tool only on those platforms (`windows` is the Windows app platform).
  Leave it out to offer the tool everywhere.
- The tool's output is redacted and bounded to 400 lines and 16 KB (16384
  UTF-8 bytes), whichever comes first. The cut never splits a line, and a
  notice at the end says how many lines and bytes were left out, so the model
  can ask for a narrower result. Return less, or let the tool take a filter.
- A name the agent already uses (`observe`, `done`, `navigate`, `back`,
  `scroll`, `scroll_to`, or an action verb) is `INVALID_CONFIG`, and so is
  `report_finding`, the tool `Explore.ExploreAsync` adds (topic `explore`).
- A tool that needs the screen takes the second `AgentTool` constructor,
  whose delegate also gets an `AgentToolContext`: `ObserveAsync` (a read-only
  tool only; a mutating tool gets `POLICY_DENIED`) and `AttachScreenshotAsync`.
- Not ported from upstream: building a tool from an AI SDK tool
  (`defineTool`, `toModelOutput`).

On a device, `MobileTools.Create(engine)` from `E2E.Mobile` builds the tool pack
upstream calls `mobileTools`: `open_app`, `swipe`, and `alert`, offered only on
the platforms of the engines you pass. Return it from `AgentTools` beside your
own tools. A step that stays within the grammar verbs replays with no model
call, so prefer the target's `App.BundleId` over `open_app` when every test
starts in the same app. `open_app` takes an optional `app`: without one it returns to the app under test (the target's `App.BundleId`, or the app the build installed), so the agent can recover after a system sheet or another app took the screen. `samples/MobileApp.E2ETests` shows it in use.

Not ported: decision models and the `--debug` agent step table. To see every model call, use
`E2E_AI_TRACE=1` (topic `debugging`).

## Custom executors

An executor runs the `ActAsync` and `AssertAsync` steps of an agent. Write one
when `System` and project tools are not enough.

| Option | You write | The runtime keeps |
| --- | --- | --- |
| `ToolLoop.CreateToolLoopExecutor` | The tools and the prompt. The loop adds only `done`. | Budgets, loop guards, verdicts, and usage counts |
| `IStepExecutor` | `RunStepAsync(context, token)`. A model is optional. | The step deadline, redacted observations, checked actions, and the verdict |
| `AcpExecutor.ClaudeCode(options)`, `AcpExecutor.Codex(options)`, `AcpExecutor.Create(command, args, options)` (`E2E.Acp`) | Nothing for a preset. `Create` takes the command of any agent that speaks the Agent Client Protocol on stdio. | The same as `IStepExecutor`. The agent gets the built-in action tools and `done` as an MCP server, and every action goes through `ctx.Actions`. A permission request fails closed, and a tool of the agent's own ends the step with `POLICY_DENIED`. |

A coding agent the user already signs in to runs agent steps with no model provider: install `E2E.Acp` and the agent's adapter, and use `AcpExecutor.ClaudeCode(new AcpAgentOptions { Model = "sonnet" })` (sign in with `claude`, or set `ANTHROPIC_API_KEY`; adapter `npm install -D @agentclientprotocol/claude-agent-acp`) or `AcpExecutor.Codex()` (sign in with `codex login`; adapter `@agentclientprotocol/codex-acp`). Node must be on the `PATH`: the preset finds the adapter in `node_modules` from the working directory up and runs it with `node`, and an adapter that is not installed is `INVALID_CONFIG`. The agent gets only the step tools: Claude Code with its own tools, settings, MCP servers, and saved sessions off, Codex read-only with its shell, web search, apps, plugins, browser, and your MCP servers off. A tool of the agent's own that runs without asking fails the step with `POLICY_DENIED`. `AcpAgentOptions` takes `Model` (a model the agent offers; the error lists them when it does not), `Env`, `System`, and `Name`. The agent's process gets your environment without the `E2E_SECRET_*` and `E2E_USER_*` variables, in any casing, from the process and from `Env`; a secret in any other variable reaches the agent. A prompt turn counts as one model call. `WaitForAsync` and `ExtractAsync` still need `Model`. A custom command must be an executable on Windows, not a `.cmd` shim. See the online guide `/features/acp/`.

`ExecutorObservation.Path` (from `context.ObserveAsync`) is where the screen is, redacted:
the path, query, and fragment of the engine's `Observation.Location`, so
`/app#/cart?id=1` keeps its fragment, and a secret marker in the query is
percent-encoded (`%3Csecret:NAME%3E`). A location that is not a URL stays as it
is; an engine that reports only a route gives that route. This follows upstream's
`observationPath`.

Set one as `AgentOptions.Executor` for a named agent, `E2ESessionOptions.AgentExecutor`
for the default agent, or override `AgentExecutor` in the fixture. A config
file cannot hold one: an executor is code. The entry still takes `Model`,
`Judge`, `Context`, and the budgets, and refuses `System` and `Tools` beside an
executor (`INVALID_CONFIG`): pass those to the executor. A model or judge the
executor brings is the agent's, and an entry that names another is
`INVALID_CONFIG`. `WaitForAsync` and `ExtractAsync` keep using the judge.

Keep the loop, change the vocabulary. This agent has two tools: one reads the
screen, one taps a node by its id.

```csharp
using System.Text.Json;
using E2E;
using E2E.NUnit;

public sealed class TapOnlyTests : E2ETest
{
    private static JsonElement Schema(string json) => JsonDocument.Parse(json).RootElement;

    protected override IAgentModel? CreateModel() => /* your model */ null;

    protected override IStepExecutor? AgentExecutor => ToolLoop.CreateToolLoopExecutor(new ToolLoopExecutorOptions
    {
        Name = "tap-only",
        System = _ => "Read the screen before acting. Tap controls by their id from the latest read.",
        BuildPrompt = context => Task.FromResult(context.Step.Instruction),
        Tools = (context, _) =>
        [
            new ToolLoopTool(
                new ModelTool { Name = "read_screen", Description = "Read the screen as text, one node per line as #id role \"name\"", Parameters = Schema("""{ "type": "object", "properties": {} }""") },
                async (_, token) => new ToolLoopResult((await context.ObserveAsync(cancellationToken: token)).Text)),
            new ToolLoopTool(
                new ModelTool { Name = "tap", Description = "Tap a node by its id from the latest read_screen", Parameters = Schema("""{ "type": "object", "properties": { "id": { "type": "string" } }, "required": ["id"] }""") },
                async (call, token) =>
                {
                    await context.Actions.TapAsync(new ExecutorTarget(call.Arguments.GetProperty("id").GetString()!.TrimStart('#')), token);
                    return new ToolLoopResult("Tapped.");
                }),
        ],
    });
}
```

The loop runs at most `MaxModelCalls` model turns per step. With five turns left
it asks the model to wrap up, and on the last two turns, after a loop-guard stop,
or when the step is nearly out of time, it offers only `done`. A step whose turns
run out without a `done` call fails with `STEP_NO_CONCLUSION` (a failed verdict,
not a hard stop). The instructions tell the model, from the first turn, to reply
with tool calls only; a reply without one is kept in the history, the model is
told to act, and the turn counts. A tool that throws
is a failed call the model reads, except a hard stop of the step, which ends
the loop. `ToolLoopHelpers.GuardAsync` gives a tool the same rule for work of its
own. `PrepareMessages` runs between turns to shorten the history, or stops the
step when it makes no progress. A message it rewrites edits the request prefix,
and on Anthropic models, whose thinking is bound to it, the loop then stops
sending the reasoning from that message on. The same holds for a history the
loop shrinks after a context overflow. `context.Actions` checks, counts, and records
each action, so these steps replay from the cache and run on every engine that
supports the verb. A tool that works outside `context.Actions` wraps its body in
`context.Budgets.RunToolAsync(name, mutates, body)`, which takes an action slot
and records the call. Do not call `context.Actions` or `context.ObserveAsync` inside a
mutating body: the call waits behind itself.

To keep the built-in actions beside your own tools, add
`AgentTools.CreateGrammarTools(context, new GrammarToolsOptions { Guard = body => helpers.GuardAsync(body) })`
to the list `Tools` returns. It gives the built-in agent's action tools for the
verbs the target's engine supports, and each result reports what changed on
screen. The built-in agent lists the screen as `#id role "name"` lines, once at the
start of the step and on `observe`; a result after an action shows only the
`added`, `changed`, and `removed` lines since the screen the model last read, and
the whole screen again when most of it changed. An action takes the id as `ref`. `DefaultAgent.BaseRules` is the built-in agent's guidance for those
tools: start `System` with it. The loop appends `AgentTools.VerdictRules` itself.
`Guard` is optional; without it a hard stop of the step leaves the tool and the
loop ends the step. A result is a `ToolLoopResult`: text, or text with a
screenshot in `Pixels`. When a tool throws an error that
`StepExecutors.IsRuntimeHardStop(error)` accepts, end the step and rethrow it.
The context must be one the runtime opened for a step.
Each built-in tool's schema, and the schema of `done`, is closed: it carries
`additionalProperties: false`, and a call with a field the tool does not declare
(`force`, `selector`) is refused with the field named, never run without it. A
tool of your own keeps the schema you give it.

```csharp
Tools = (context, helpers) =>
[
    .. AgentTools.CreateGrammarTools(context, new GrammarToolsOptions { Guard = body => helpers.GuardAsync(body) }),
    ReadInbox(context),
],
System = _ => DefaultAgent.BaseRules + "\n\nCheck the inbox with read_inbox when a step mentions an email.",
```

An executor that drives a model outside the loop gets the built-in verdict from
`AgentTools.CreateVerdictTool()`: give the model its `Tool` and
`AgentTools.VerdictRules`, run each `done` call with `Execute(call)`, and return
`Verdict` once `Concluded`. The tool is `done`, and it takes `code`.

The observation's `KeyboardVisible` says whether an on-screen keyboard is
showing, and is `null` when the engine can't tell.

Replace the executor. This one uses no model and supports one assertion:

```csharp
public sealed class ReadyExecutor : IStepExecutor
{
    public string Name => "scripted";

    public ExecutorCache Cache => ExecutorCache.Off;

    public async Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken cancellationToken)
    {
        if (context.Step.Kind != ExecutorStepKind.Assert || context.Step.Instruction != "the screen says Ready")
        {
            return new StepVerdict(StepVerdictStatus.Blocked, "This executor only checks whether the screen says Ready.", "AUTOMATION_UNSUPPORTED");
        }

        var screen = await context.ObserveAsync(cancellationToken: cancellationToken);
        return screen.Text.Contains("Ready", StringComparison.Ordinal)
            ? new StepVerdict(StepVerdictStatus.Passed, "The screen says Ready.")
            : new StepVerdict(StepVerdictStatus.Failed, "Ready is absent from the screen.");
    }
}
```

What the context holds:

| Member | What it is |
| --- | --- |
| `Step` | The `Kind`, `Instruction`, `Params`, `Index`, and `Secrets`. A secret value the attempt knows reads `<secret:name>`, and a `Secret` in the params is its name and purpose, never its value |
| `ObserveAsync(options)` | Redacted screen text, one node per line as `#id role "name" ...` (upstream's `text`; an executor that parsed the earlier `[ref=...]` text reads the id after `#` now; on the web an empty painted rectangle, at least 12 CSS pixels on each side, such as a canvas node or a drop zone, reads as role `box`), and with `Tree` the node tree (`ExecutorNode`, whose `Id` is the id) and with `Pixels` a masked screenshot, or `PixelsWithheld` with the reason |
| `Actions` | Checked and recorded verbs: `TapAsync`, `DoubleTapAsync`, and `SecondaryTapAsync` (all take `new ClickOptions { Modifiers = [KeyModifier.Shift] }` when `Target.TapModifiers`), `LongPressAsync`, `HoverAsync`, and `DragAsync(source, destination)` (when `Target.Verbs` holds `ExecutorVerb.LongPress`, `SecondaryTap`, `Hover`, or `Drag`), `TypeAsync`, `TypeSecretAsync`, `ClearAsync`, `PressAsync`, `SelectAsync`, `CheckAsync`, `UploadAsync(target, paths)` (when `Target.Verbs` holds `ExecutorVerb.Upload`), `ScrollToAsync`, `ScrollUntilAsync`, `ScrollAsync`, `NavigateAsync`, `BackAsync`, `ResizeAsync(size)` (when `Target.Verbs` holds `ExecutorVerb.Resize`). A node is `new ExecutorTarget(ref)` |
| `Target` | The target's `Name`, `Platform`, and `Verbs`, the verbs its engine supports, and `TapModifiers`, true when `TapAsync` and `DoubleTapAsync` take `ClickOptions.Modifiers`. Offer a model only these |
| `ReplayedPrefix` | Set when a cache replay ran some or all of the step's actions first (all of them after an `end-mismatch`). The actions and why it stopped (the agent's prompt reads `Replay stopped (<reason>) after N of M recorded actions.`, plus a warning for `end-mismatch`, and one when the next action failed with an unknown commit state). Continue from the current screen without repeating them |
| `Model`, `ProviderOptions`, `AgentContext`, `Ledger` | The agent's model and provider options, its project context, and the steps completed before this one, as numbered lines (`1. app.open passed :: /`) in at most 8192 bytes |
| `Attempt` | `TestId`, `AttemptId`, the test's `TitlePath` (empty outside a test), `Index`, a `CancellationToken` that ends with the attempt, and a `Memory` dictionary the runtime never reads |
| `Budgets` | `MaxActions`, `MaxModelCalls`, `ActionsUsed()`, `RemainingMs()`, `RecordModelCall(ExecutorModelCall)`, and `RunToolAsync`. `ExecutorModelCall.Reasoning` is the model's own reasoning for the turn; the step's `model` event carries a redacted excerpt of at most 600 characters, and a turn with no reasoning leaves the field out |
| `AttachTranscript`, `AttachScreenshotAsync` | Keep a model transcript, or observed pixels (a `screenshot` artifact of the step; it returns the artifact's report id), under the attempt's results directory |
| `AttachTurns(turns)` | Keeps a clipped record of the model turns (`StepTurn`: `Index`, `Calls`, `Outcome`) on the step. The trace page shows the last ones under a failed step. Call it as each turn ends; the last call wins. A secret the attempt knows is redacted. `ToolLoop.CreateToolLoopExecutor` calls it for you |

`RunStepAsync` gets a token that ends on cancellation, at the step deadline, and
on any other hard stop. An executor that ignores it still cannot outlive the
clock: a second after the stop, the runtime settles the step without it.

The verdict is `Passed`, `Failed`, or `Blocked` with a summary. A blocked
verdict needs a code from `StepExecutors.BlockableCodes`, and a passed one has
none. Return `STEP_BUDGET_EXHAUSTED`, `STEP_TIMEOUT`, or `CANCELLED` only when
that budget is spent, the deadline passed, or the test was cancelled: any other
use is `MODEL_OUTPUT_INVALID`, and so is an empty summary. A verdict never
outranks a hard stop of the step.

`Cache` sets how the executor's act steps use the replay cache. `Inherit`, the
default, follows the configured mode: a recorded step replays without calling
the executor, and when a replay stops part way the executor gets
`ReplayedPrefix`. `Off` never replays or records. An assert is never cached.

On an agent with an executor, `AssertAsync` goes to it as a step with `Kind`
`Assert`. `Vision` is refused with `UNSUPPORTED_CAPABILITY`, since the executor
decides what its model sees. `Screenshot` still keeps evidence after the
verdict.

A custom executor shows an image in a tool result with `ToolLoopResult.Pixels`;
the loop sends it on a user turn after the turn's tool results. Set
`ToolLoopExecutorOptions.BuildMessages` instead of `BuildPrompt` to open the
step with images (exactly one of the two).

Not ported: the observation's `Revision`.

## In CI

Run the suite, agent steps included, on every pull request, with the
provider's API key from the CI secrets. A committed `.e2e/cache/` lets CI
replay verified acts with no model call (topic `running`).
