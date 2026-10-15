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
  `system`, `context`, `maxSteps`, `maxModelCalls`, `judgmentTimeout`, and
  `providerOptions`. None inherits from `default`.
- `model` drives `ActAsync`. Judgments use `judge` when set, else `model`.
- No model and no replay that finishes the step is `MODEL_UNAVAILABLE`.
  Auth failures surface on the first model call as `MODEL_PROVIDER_FAILED`.
- `context` is what the app calls things, sent to every model call, judges
  included. `system` is how the acting agent works; judges never see it.
- In code, `ModelProviders` and `Subscriptions` build the same clients, and a
  fixture can override `CreateModel()` and `CreateJudge()`.
- A custom `IAgentModel` reads each `ModelMessage.Images` of a `user` turn
  (`ModelImage`: `Data`, `MediaType`, `Width`, `Height`). The five built-in
  providers send them after the turn's text. The agent sends none yet.

### Choose an agent

Tests use `agents.default`. A call picks another with its `Agent` option:
`new ActOptions { Agent = "buyer" }`. An unknown name is `INVALID_ARGUMENT`.
There is no `--agent` flag and no pin for a whole test; name the agent on
each call.

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
`Cache` (the replay cache's part). A failed or blocked step throws an
`AgentException` whose `Code` says why: `ACTION_FAILED` (product failure);
`STEP_BUDGET_EXHAUSTED`, `STEP_TIMEOUT`, `CONTEXT_OVERFLOW` (out of room);
`AUTH_CREDENTIAL_UNAVAILABLE`, `AUTH_CREDENTIAL_INVALID`,
`SECRET_UNAVAILABLE`, `ENVIRONMENT_UNAVAILABLE`, `SEED_DATA_MISSING`,
`TEST_SETUP_FAILED`, `AUTOMATION_UNSUPPORTED`, `POLICY_DENIED` (blocked from
outside, `Blocked` is true); `MODEL_OUTPUT_INVALID` (unusable answer). Full
list: topic `debugging`.

`ActOptions`:

- `Params`: values the instruction names. The runner fills a `Secret`, and a
  `Values.Unique(...)` value keeps the cache working across runs.
- `Timeout`: 30 s by default (`StepTimeout` on the fixture).
- `MaxSteps` (default 25 actions), `MaxModelCalls` (default 25): may only
  lower the agent's limits, else `INVALID_ARGUMENT`.
- `Agent`: the named agent that runs the step.

The tools the model can call: `observe`, `tap`, `double_tap`, `fill`,
`fill_secret` (a `Secret` from `Params`, by name), `press`, `select`,
`check`, `uncheck`, `clear`, `scroll`, `scroll_to`, `navigate`, `back`, and
`done`. `scroll` and `scroll_to` come with an engine that can scroll, and
`back` with one that has history. There is no `hover`, `drag`, `upload`,
`long_press`, `right_click`, `screenshot`, or point tool: a step that needs
one is `AUTOMATION_UNSUPPORTED`. Do that part with `Screen` actions instead.
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
- Judgments see the statement and the current screen only, never prior
  steps. Malformed output gets one repair round, then
  `MODEL_OUTPUT_INVALID`.
- A value shown in more than one place (a total in the summary and on the
  pay button) must agree everywhere, or the judgment fails. Name the one you
  mean (`"the order summary total is $42.00"`) when only it matters.
- `WaitForAsync` judges at once, then every `Interval` (default 3 s) only
  when the screen changed, and is `STEP_TIMEOUT` after `Timeout` (default the
  agent's `judgmentTimeout`, 30 s).
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
- After a `Secret` is filled, no pixels leave the session. `On` then judges the
  snapshot alone. `Only` fails with `POLICY_DENIED` rather than answer from the
  snapshot, and so does an engine that captures no pixels or masks fewer
  regions than the snapshot has secure fields.
- An inconclusive judgment made without pixels ends with a hint to pass
  `Vision = VisionMode.On`, unless pixels cannot help (a secret was filled, or
  the engine captures none).
- `AssertAsync` keeps a redacted screenshot of the screen it judged under
  `.e2e/results/<test>/attempt-<n>/screenshots/`, and
  `AgentException.Screenshot` names it on a failure. Set
  `AssertOptions.Screenshot = false` to keep none. After a secret fill none is
  kept.

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
  `AssertAsync`, or `WaitForAsync` passes, so an unchecked act never
  replays. Locator reads, `locator.WaitForAsync`, and `ExtractAsync` verify
  nothing.
- A replay re-finds each control by role, name, test id, and path, and
  needs the recorded start route unless the recording opens with a
  navigation. It passes alone only when the recorded end route is back,
  every control that appeared (with its text, value, and checked or
  selected state) is there, every one that went away is gone, at least one
  of those changed during the replay, and no new alert showed; otherwise
  the agent takes over mid-step. The web engine does not observe Next.js
  App Router's route announcer, so its title echo never counts. Text that
  reads differently on every run, a date, a time, an id, is not checked.
  Entries are keyed per agent and per agent `context`. `ActResult.Cache.Reason` says why a replay missed:
  `no-entry`, `invalid-entry`, `wrong-context`, `target-not-found`,
  `target-ambiguous`, `end-mismatch`, `gap` (the recording stops at a
  project tool, see below).
  `ActResult.Cache.Detail` says where the replay stopped and why
  (`at action 2 of 3, tap button "Apply": ...`). After an `end-mismatch` it
  names what did not hold: the recorded nodes that did not show, the ones that did not go away, and
  any alert the recording never saw, or says the outcome already showed
  before the replay acted.
- A step recording no actions, or changing nothing on screen or in the
  route, creates no entry. The screen it passed on is read once it holds
  still, so a late render still counts. One whose `Values.Unique` value
  equals another param's value is not recorded either
  (`ActResult.Cache.NotRecorded`: `param-collision`).
- Only the first attempt replays. A `[Retry]` attempt runs live and still
  records.
- Commit `.e2e/cache/` to share replays with CI and teammates.
- `cache.strict` fails a recording that no longer replays with
  `REPLAY_STALE`, also when the recording sits under an old key (topic
  `running`). Re-record with a run without it, then commit.

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
- `platforms: ["ios", "android"]` offers the tool only on those platforms.
  Leave it out to offer the tool everywhere.
- The tool's output is redacted and cut at 16384 characters.
- A name the agent already uses (`observe`, `done`, `navigate`, `back`,
  `scroll`, `scroll_to`, or an action verb) is `INVALID_CONFIG`.
- Not ported from upstream: building a tool from an AI SDK tool
  (`defineTool`, `toModelOutput`), and `getToolContext`, so a tool cannot
  observe the screen.

On a device, `MobileTools.Create(engine)` from `E2E.Mobile` builds the tool pack
upstream calls `mobileTools`: `open_app`, `swipe`, and `alert`, offered only on
the platforms of the engines you pass. Return it from `AgentTools` beside your
own tools. A step that stays within the grammar verbs replays with no model
call, so prefer the target's `App.BundleId` over `open_app` when every test
starts in the same app. `samples/MobileApp.E2ETests` shows it in use.

Not ported: a custom `executor`, decision models, `maxObservationBytes`,
`maxInputTokens`, `--debug`, and `--ai-trace`.

## In CI

Run the suite, agent steps included, on every pull request, with the
provider's API key from the CI secrets. A committed `.e2e/cache/` lets CI
replay verified acts with no model call (topic `running`).
