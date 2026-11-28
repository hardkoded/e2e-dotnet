# Writing tests

## Contents

- Pick your framework
- Structure: one class, arrange-act-assert, one behavior
- Fixtures
- Locators
- Expect
- Sign-in
- Start near the screen under test
- The browser fixture
- Habits

## Pick your framework

A test class derives from an `E2ETest` base class. Its setup, attributes,
model-test marker, and `dotnet test --filter` syntax depend on the
framework; read the guide for yours first:

| Framework | Package | Topic |
| --- | --- | --- |
| NUnit | `E2E.NUnit` | `writing-tests-nunit` ([writing-tests-nunit.md](writing-tests-nunit.md)) |
| xUnit v3 | `E2E.XUnit.V3` | `writing-tests-xunit` ([writing-tests-xunit.md](writing-tests-xunit.md)) |
| TUnit | `E2E.TUnit` | `writing-tests-tunit` ([writing-tests-tunit.md](writing-tests-tunit.md)) |

This topic covers what they share. A test body reads the same in each:

```csharp
await App.OpenAsync("/todos");

// An exact interaction: an empty submit adds nothing.
await Screen.GetByRole("button", "Add").ClickAsync();
await Expect.That(Screen.GetByRole("listitem")).ToHaveCountAsync(0);

await Agent.ActAsync("add a todo with the given title", new ActOptions
{
    Params = new Dictionary<string, object?> { ["title"] = "Write the release notes" },
});

await Expect.That(Screen.GetByRole("listitem")).ToHaveCountAsync(1);
```

The agent drives the flow. A passing `Agent.AssertAsync` or locator
matcher checks the outcome and confirms the preceding actions for replay.
`Screen` actions are for low-level mechanics under test (like the empty
submit above) or configs with no model; a goal takes its values in
`Params`. Always read "Structure" next. Every test starts from a
fresh browser context with no page open, so it calls `App.OpenAsync` first.
There is no `describe`, `test.setup`, or tag of e2e's own: grouping, setup, and
categories are the framework's. For a serial group whose members share one
session (the page state of one member is still there in the next), mark an
NUnit fixture `[Serial]` and order its tests with `[DependsOnTest]`. The session
starts with the first member and ends with the group, and a member that fails
skips the rest. xUnit has no such attribute.

## Structure: one class, arrange-act-assert, one behavior

Write every test so a failing name says what broke.

- **One test class per feature.** Name it after the feature (`TodosTests`,
  `CheckoutTests`) and put every test for it inside, with the setup those
  tests share (NUnit `[SetUp]`, xUnit v3 `InitializeAsync`). A file holds
  one feature. Split a class only to share a setup or an attribute with
  some of its tests.
- **Arrange, act, assert in order.** In the class, put the shared `const`s
  and fields first, then the setup, then the tests. Inside a test, keep
  arrange, act, and assert apart with a blank line:
  - **Arrange:** reach the starting state through the framework's setup,
    `App.OpenAsync` on the screen's path, or data from the app's API. End UI
    setup with a wait (`locator.WaitForAsync()`, `Agent.WaitForAsync()`) so
    setup fails there and confirms the setup `Agent.ActAsync` steps for the
    replay cache.
  - **Act:** pursue the test's one goal. Start with one `Agent.ActAsync`
    call that spells out the required fields, choices, buttons, and order.
    Split it only when a later step needs a known screen state, when a
    long flow risks the `MaxSteps` or `MaxModelCalls` budget, or when a
    failure in the middle needs its own step boundary: explain why in a
    comment and wait for the intermediate screen state. Before leaving a
    screen or calling `Agent.AssertAsync`, wait for the required state.
  - **Assert:**
    - Put every `Expect.That`, `Expect.Poll`, and `Agent.AssertAsync` here,
      with nothing after them.
    - Never assert mid-way: wait on a narrowed locator instead, such as
      `balance.Filter(hasText: new Regex(@"^\$10\.00$")).WaitForAsync()`
      (an anchored `hasText` on a leaf status or value node; use
      `Filter(has: Screen.GetByText(name))` for exact row matching),
      `Screen.GetByRole("switch", "Lock card", new RoleOptions { Checked = true }).WaitForAsync()`,
      or `heading.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Hidden })`
      after an earlier positive wait (`Hidden` passes at once on zero
      matches, a typo included, and still confirms the earlier
      `ActAsync` steps).
    - Never move a pre-reload or pre-navigation check past the reload or
      navigation: split the earlier phase into its own test. Never stash a
      one-shot read (`IsVisibleAsync`, `IsEnabledAsync`, `CountAsync`,
      `InputValueAsync`, `TextContentAsync`, `GetAttributeAsync`) mid-flow
      in a local variable just to assert it at the end: leave a
      precondition as a `WaitForAsync()` alone, split the earlier phases so
      retrying matchers run on the live screen, or use `Expect.Poll(...)`.
      When a computed arrange precondition is missing, throw directly
      instead of inventing a fallback value.
    - On a zero-match timeout, a wait reports `LOCATOR_NOT_FOUND` and a
      check `ASSERTION_FAILED`; two matches throw `LOCATOR_AMBIGUOUS` on
      either.
- **One behavior and one goal per test.** The name states one outcome
  (`Marks_a_todo_as_done`, not `Adds_and_completes_a_todo`). Several
  assertions that check that one outcome are fine; a second behavior is a
  second test that arranges its own starting state. Never undo an action
  before asserting its effect (returning to the starting value hides a
  no-op), and never leave a screen before checking what the test promised
  on it.
- **Prove the change on the changed record.** Ask what wrong behavior would
  still pass (a no-op, a deleted row, the wrong row, an unsaved reload).
  Pick an end value the start cannot show, write expected values as
  literals, and scope checks to the record's row, card, or dialog (never
  `First()`). When persistence is part of the promise, wait for the
  confirmation, reopen the screen afresh, and compare against the inputs
  (and read the record again by id from the API when there is one). Check
  only the promised behavior; the name claims only what the assert checks.
- **Act on the second item shown, never the first.** When a test removes,
  edits, renames, completes, or toggles one item of a list (a row, a line,
  a card), its arrange block adds two items, and its act picks the second
  item the list shows. In a list that shows the newest item first, that is
  the item added first. Its assert block then checks that the first item is
  still there, unchanged. An app that always changes the first row passes a
  test that acts on the first row.
- **Refusals and multi-user delivery.** When a refusal promises no persisted
  write, wait for the refusal message (or assert it in its own test), reopen
  after an independent readiness wait, and assert the records stayed
  unchanged; put a follow-up retry in its own test. For recipient delivery,
  keep a separate recipient session open and scope the checks to the
  run-unique message (and the history after a reopen, if promised).
- **Each test owns its starting state and passes alone.**
  - **Client vs. server state:** every test starts from a fresh browser
    context, so nothing the browser keeps leaks from an earlier test. On an
    Android or iOS target, reset with `App.ClearStateAsync()` (topic
    `mobile`).
  - **Server data:** create the records a test needs in the framework's
    setup with a run-unique name (`Guid.NewGuid()`), take the id from a 2xx
    response before asserting the exact status, and delete the record in the
    teardown, which runs after a failure too. A `try`/`finally` around the
    creation and the rest of the setup cleans up a partial setup once you
    hold a usable id. Even when a test deletes its record, keep the teardown
    and accept `404`, so an earlier failure never leaks. Guard the teardown
    with the id (or a `created` flag), so a setup that failed before it
    created anything touches no shared state. Without a test API, seed through
    the UI in the setup and delete through the UI in the guarded teardown;
    never clean up at the end of a test body.
  - **No coupling or unwaited branching:** never chain tests across shared
    state, never use `[Serial]` to hide coupling, and never branch or skip
    on `IsVisibleAsync()` or `CountAsync()`, which do not wait. Every test
    must pass when run alone, by its filter, twice in a row.
- **Name and reuse what the tests repeat.** Check the existing tests,
  helpers, and page objects first and reuse or extend them instead of
  duplicating setup. Define a repeated selector input, value, or prompt once
  as a `const` (or a feature-local helper class as the tests grow) and keep
  a single-use one inline; bind a repeated locator to a local variable. Pass
  variables to prompts in `Params` (`Values.Unique(...)` for a run-unique
  value, so the replay cache hits).
- **Prefer `Agent.ActAsync` when a model is configured.** When
  `e2e.config.json` configures a model, or code sets a model or a custom
  executor (`AgentOptions.Executor`, `E2EConfig.WithExecutor`; the JSON file
  cannot hold an executor), on the default agent (or on a named agent under `agents`, picked as topic `agent`
  explains), default to `Agent.ActAsync` for user flows (forms, buttons,
  list and CRUD mutations, navigation, sign-in, with the values in
  `Params`), and do not avoid it on a new user flow merely because an
  `[E2ESetup]` test or a sign-in helper uses `Screen` actions. Pair it with
  verifying checks at the end, so confirmed `ActAsync` steps record and
  replay without new model calls (a model-backed judgment such as
  `Agent.AssertAsync` still calls the model on a rerun). Reserve `Screen`
  actions for low-level mechanics that are themselves under test (the
  empty submit in the example above, a keyboard shortcut, a drag offset), a
  sign-in setup test, or a project with no model configured. Use `Expect`
  for exact values, counts, or field states; use `Agent.AssertAsync` alone
  when the screen shows the full semantic or visual outcome, and pair a
  deterministic `Expect` when exact phrasing or state must also hold
  (topic `debugging`, "Flaky tests"). A passing locator or engine matcher,
  `locator.WaitForAsync`, `Agent.AssertAsync`, or `Agent.WaitForAsync`
  confirms the `ActAsync` steps before it for the replay cache;
  `Expect.Poll` and a plain-value check confirm nothing.

```csharp
public sealed class TodosTests : E2ETest
{
    private const string Title = "Write the release notes";
    private const string Other = "Book the venue";
    private const string AddTodo = "add a todo named {title}";

    [Test]
    public async Task Adds_a_todo_to_the_list()
    {
        await App.OpenAsync("/todos");

        await Agent.ActAsync(AddTodo, new ActOptions
        {
            Params = new Dictionary<string, object?> { ["title"] = Title },
        });

        await Expect.That(Screen.GetByRole("checkbox", Title)).Not.ToBeCheckedAsync();
        await Expect.That(Screen.GetByRole("listitem")).ToHaveCountAsync(1);
        await Expect.That(Screen.GetByRole("status", "Remaining")).ToHaveTextAsync("1 remaining");
    }

    [Test]
    public async Task Marks_a_todo_as_done()
    {
        var todo = Screen.GetByRole("checkbox", Title);
        await App.OpenAsync("/todos");
        await Agent.ActAsync(AddTodo, new ActOptions
        {
            Params = new Dictionary<string, object?> { ["title"] = Other },
        });
        await Agent.ActAsync(AddTodo, new ActOptions
        {
            Params = new Dictionary<string, object?> { ["title"] = Title },
        });
        await todo.WaitForAsync();

        // The second todo shown: this list adds new todos at the end.
        await Agent.ActAsync("mark {title} as done", new ActOptions
        {
            Params = new Dictionary<string, object?> { ["title"] = Title },
        });

        await Expect.That(todo).ToBeCheckedAsync();
        await Expect.That(Screen.GetByRole("checkbox", Other)).Not.ToBeCheckedAsync();
        await Expect.That(Screen.GetByRole("status", "Remaining")).ToHaveTextAsync("1 remaining");
    }
}
```

The bad version of a list mutation adds `Other`, then `Title`, and acts on
`Other`, the first item shown: an app that always acts on row 1 passes it.
The good one above acts on `Title`, the second, then checks that `Other`
is unchanged.

For the replay cache (topic `agent`), a passing locator or engine matcher,
`locator.WaitForAsync`, `Agent.AssertAsync`, or `Agent.WaitForAsync`
confirms every `ActAsync` before it; `Expect.Poll` and a plain-value check
confirm nothing, so end with a locator check too. When the test fails,
every goal after the last passing confirmation runs live again on the next
run: the arrange block's closing wait keeps the setup goals cached while
you fix the act or the assertions.

## Fixtures

Members of `E2ETest` in both frameworks:

- `App`: `OpenAsync(path)`, `BackAsync`, `RestartAsync`, `ClearStateAsync`,
  `ScreenshotAsync(label)`, and `BaseUrl` (the target URL with the run's port and no trailing slash, so `$"{App.BaseUrl}/api/users"` joins with one slash; null when the target declares no `app.url`). A path resolves against the target URL. `OpenAsync` and the
  agent's navigate step admit only `http:`, `https:`, and the exact
  `about:blank`; any other scheme is `POLICY_DENIED`. `RestartAsync` keeps
  cookies and storage, `ClearStateAsync` drops them, and both reopen the
  target URL inside their own step, so the report shows `app.restart` or
  `app.clearState` alone, with no `app.open` after it. `ClearStateAsync` is
  `UNSUPPORTED_CAPABILITY` with `Connect.ReconnectEndpoint` or an
  attempt-scoped `Browser` provider. `ScreenshotAsync(label)` saves a redacted
  screenshot artifact under `screenshots/` and returns its absolute path; the
  report records the path relative to the artifact root. It masks secure
  fields only, after a `Secret` fill too, so a secret the app shows anywhere
  else is in the image. A screenshot taken while the page navigates captures
  the document showing. It is `UNSUPPORTED_CAPABILITY` only on an engine that
  keeps no screenshot artifact. The web, mobile, and Windows engines keep
  screenshot artifacts.
- `App.StartRecordingAsync(new RecordingOptions { Name = "dialog" })`, then
  `StopRecordingAsync()`: records one span of the screen, such as a single
  transition, and returns its videos in play order (`RecordedVideoFile.Path`,
  absolute, under the attempt's `recordings/` as `001-<name>.<ext>`, or
  `RecordedVideoLink.Url` when a hosted browser or device keeps the recording).
  There is one video, except on the web when the page was replaced while it
  recorded (`RestartAsync`, `ClearStateAsync`): then one per page. The step
  links each video in the trace. One recording at a time (a second start is
  `INVALID_ARGUMENT`, as is a stop with none running), and none while the
  test's `video` records, since the engine has one recorder: that start is
  `UNSUPPORTED_CAPABILITY`, so leave `video` off for the test. A recording
  still running when the test ends is stopped and kept. Like the test's
  video, it masks nothing.
- `Agent`: `ActAsync`, `AssertAsync`, `WaitForAsync`, `ExtractAsync<T>`
  (topic `agent`).
- `Screen`: the locators below.
- `Expect`: `Expect.That(locator, message?)`, `Expect.That(Browser)`, `Expect.Soft(locator)`, `Expect.Soft(screen)`, `Expect.Poll(read)`, `Expect.That(block).ToPassAsync()`.
- `Browser`: URL, title, cookies, viewport, raw keyboard and mouse, and
  responses (below).
- `Secrets.Get(name)`: a `Secret` from the config. `Credentials.User(name)`:
  an account from `E2E_USER_<NAME>_USERNAME` and `_PASSWORD`.

Guard a teardown (NUnit `[TearDown]`, xUnit v3 `DisposeAsync`) with the id
or a `created` flag (or clean up only what this attempt wrote), so a skipped
attempt, or one that failed before it mutated anything, never touches shared
state. To route tests across same-platform targets
(such as desktop and mobile viewports), mark the tests with the framework's
category or trait and run each target with `E2E_TARGET=<name>` and that
category filter, rather than sniffing the viewport width or the time zone in
the setup and skipping: that boots every test on every target and turns a
misconfigured target into a green skip.

## Locators

- `GetByRole(role, name)`, `GetByText`, `GetByLabel`, `GetByPlaceholder`,
  `GetByTestId`, and `GetByDisplayValue`. Prefer a role with the accessible
  name the app renders.
- For a collection check, start at the owning list or table and query only
  its items or rows. When the requirement names the whole collection, check
  every row in that container, duplicates included. If the order can change,
  sort copies before comparing the complete observed and expected lists.
- The second argument to `GetByRole` is the element's accessible name. For
  visible text inside an unlabelled list item or status, use
  `GetByRole("listitem").Filter(hasText: message)` or scope a
  `GetByText(message)` query to the right part of the screen.
- Text matches are **exact by default**. Pass `exact: false` for a
  case-insensitive substring, or a .NET `Regex`. On the web a `Regex` runs in
  the page as the JavaScript regular expression of the same pattern (its
  `IgnoreCase`, `Multiline`, and `Singleline` options travel as flags), over
  the raw text rather than whitespace-normalized text, so write a pattern both
  syntaxes read the same way: no `\z` or `\A`.
- `GetByText` and `GetByLabel` return the innermost match, so a container
  echoing its child does not count twice: `GetByText("$42.00")` finds the
  `<strong>` in `<p>Order total: <strong>$42.00</strong></p>`. On a web page,
  `GetByText` matches the DOM text only: the `<label>` answers, not the control
  it names, and a placeholder, a title, or an `aria-label` is no text.
  `GetByLabel` finds a control by its `aria-label`, its `aria-labelledby`
  target, or its `<label>`.
- Roles follow HTML-AAM, and `img` aliases `image`. An inline `<svg>` is an
  `image`. An `<img alt="">` is decoration and matches nothing, unless it has
  a `title`, a `tabindex`, or a global ARIA attribute: then it is an `image`.
- `role="presentation"` and `role="none"` are ignored on a focusable element
  or one with a global ARIA attribute such as `aria-label`, as `GetByRole`
  reads them: `<button role="none">` is still a `button`.
- A `<th>` without `scope` beside a data cell with text or children is a
  `rowheader`; one in a row of header cells, or with `scope="col"`, is a
  `columnheader`.
- A role's name is the accessible name, as Playwright computes it. Only
  content roles (button, link, cell, heading, tab, option, and the like) are
  named by their text, so `GetByRole("group", "More")` misses a `<details>`.
  A table is named by its `<caption>`, a fieldset by its `<legend>`, a figure
  by its `<figcaption>`, and an svg by its `<title>` child, which also names
  a link or button around it. A node with no name reports `Name` null.
  A content role reads its descendants, a custom element's shadow tree
  included. A descendant with a role of its own that is not text or structure
  (a `status` badge, a tree item's child `group`, an `img`) adds only its
  label or `title`: a tab holding `Inbox <span role="status">3</span>` is
  `Inbox`. An `<input type="submit">` or `type="reset"` with no `value` and no
  other accessible name reads `Submit` or `Reset`.
- `RoleOptions` adds `Checked`, `Disabled`, `Selected`, `Expanded`,
  `Pressed`, and `Level`.
- Narrow with `Filter(text)` (a case-insensitive substring, unlike a query),
  `Filter(locator)`, or `Filter(hasText:, hasNotText:, has:, hasNot:)`, which
  takes any of the four by name and needs at least one. `hasNotText` excludes
  matching subtree text, and `hasNot` drops a match whose subtree resolves the
  inner locator. Pick with `First()`, `Last()`, or `Nth(i)` when the order is
  the point. An empty `Filter()` is `INVALID_LOCATOR`. A locator also offers
  every query, scoped to its matches' descendants.
- `a.Or(b)` matches either, still strict: add `First()` when both may
  match. Both locators must come from the same screen and attempt.
- A locator resolves when it is used. A match on two nodes fails with
  `LOCATOR_AMBIGUOUS`; no match fails with `LOCATOR_NOT_FOUND`.
- `Browser.Locator(css)` and `Browser.FrameLocator(css)` (see the browser
  fixture) reach a control a role, label, or text query cannot name, and an
  iframe. Use a selector only when those can't find the element. A plain
  query never enters an iframe: query its content through `FrameLocator`.
- `Visible = true` (on `TextMatchOptions` or `RoleOptions`) drops nodes the
  page hides (a closed drawer) before the exactly-one rule. Visibility is
  what renders, as `ToBeVisibleAsync` reads it: `display: none`, a
  `visibility` other than `visible`, and content under
  `content-visibility: hidden` or a closed `details` body (all but its first
  `summary`) are hidden, through shadow
  roots and slots too, unless the page's CSS still shows them (a closed
  `details` body through `::details-content`, `content-visibility: hidden` on an
  element whose box skips nothing). A child that
  sets `visibility: visible` under a hidden parent is visible, and a
  `display: contents` element is visible when something under it is. An
  `aria-hidden` node that paints is visible, as upstream, but the tree leaves
  it out, so role queries and the agent never see it. Everything inside a hidden `<iframe>` is hidden too, cross-origin frames
  included. Unlike upstream, a box with no size reads as visible.
- On the web a `contenteditable` editing host, the root a TipTap, ProseMirror,
  Lexical, or Slate editor renders into, is a `textbox` for the agent. Its value is its text as rendered, spaces and newlines kept, so
  `ToHaveValueAsync` and `InputValueAsync` read it and `FillAsync` fills it;
  an empty editor reads `""`. It is named by its `aria-label`,
  `aria-placeholder`, or the `data-placeholder` its editor paints, and the
  blocks inside it are content, not textboxes. A native control or a drawn
  surface made editable keeps its own role. `GetByRole("textbox", name)` finds
  a host only with `role="textbox"` on it, as upstream's does; reach a bare
  host with `GetByLabel` or `GetByTestId`. Anchor a test
  on a label or a test id, since a name taken from a block's
  `data-placeholder` goes once the editor has content.
- `GetByDisplayValue` never matches a checkbox or radio. An accessible name
  includes an embedded control's value (`<button>Flash the screen
  <input value="3"> times</button>` is "Flash the screen 3 times") and leaves
  out `aria-hidden` parts, such as a required-field `*`.

### Actions

`ClickAsync` (and `TapAsync`), `DoubleTapAsync` (and `DblClickAsync`),
`SecondaryTapAsync` (a right click), `LongPressAsync`, `HoverAsync`,
`DragToAsync(target)`, `SwipeAsync`, `FillAsync` (a string or a
`Secret`), `PressSequentiallyAsync`, `PressAsync`, `CheckAsync`,
`UncheckAsync`, `ClearAsync`, `SelectOptionAsync` (a value or label, or a `SelectOption` with one of
`Label`, `Value`, or `Index`),
`FocusAsync`, `BlurAsync` (web only: `UNSUPPORTED_CAPABILITY` on a device), `SetInputFilesAsync`, and `ScrollIntoViewAsync`. Each waits up to
the action timeout for exactly one enabled, visible match. `UncheckAsync` on a
checked native radio fails with `ACTION_FAILED`; a `role="radio"` is clicked,
and fails only if it stays checked.

`PressAsync(key)` takes zero or more modifiers (`Shift`, `Control`, `Alt`, `Meta`,
`ControlOrMeta`) and one key joined by `+`, where the key is a single character
or a name (`Enter`, `Escape`, `Tab`, `Backspace`, `Delete`, `Insert`, `Space`,
the arrows, `Home`, `End`, `PageUp`, `PageDown`, `F1` through `F12`): `"Enter"`,
`"Control+a"`, `"Shift+Tab"`, `"Shift++"` (`+` with Shift). Any other key,
such as `"Ctrl+a"`, `"ab"`, or `""`, fails with `INVALID_ARGUMENT` before the
locator is resolved, on every engine; the agent's `press` is checked the same way
before any action is performed.

`FillAsync` sets the value with no key events. When the app reacts to
keystrokes (autocomplete, a masked input), use `PressSequentiallyAsync(text)`:
it focuses the one matching field and types through the engine's keyboard,
appending to the current content. With no `Delay` the whole text is one
keyboard call; with `new PressSequentiallyOptions { Delay = ... }` each code
point is its own call, with the delay waited between them. The focus, the
pauses, and the keystrokes share the one action `Timeout`: a pause is cut at it,
no character is sent past it, and the call fails with `ACTION_FAILED` saying
how many characters it typed. An engine that declares no `Keyboard`, or does
not list `focus` in `ActionKinds`, fails with `UNSUPPORTED_CAPABILITY` before
any node resolves. A `Secret` has no overload: pass it to `FillAsync`.

`ClickAsync` and `DblClickAsync` run the same action as `TapAsync` and
`DoubleTapAsync` (the engine receives `Tap` and `DoubleTap`), but record their
own step: `locator.click` and `locator.dblclick`, as in a report or trace.

`TapAsync`, `DoubleTapAsync`, and `SecondaryTapAsync` take a `ClickOptions`
whose `Modifiers` (`Shift`, `Control`, `Alt`, `Meta`, `ControlOrMeta`) are held
for the click. Only the web engine takes them; any other engine fails with
`UNSUPPORTED_CAPABILITY` before it resolves the node. A list that repeats a key
is `INVALID_ARGUMENT`. `LongPressAsync(new LongPressOptions { Duration = ... })`
holds from 100 ms through 10 s (a whole number of milliseconds, else an
`ArgumentOutOfRangeException`); without one the engine holds its own default
(500 ms on the web, one second on a device or a Windows app). `DragToAsync`
resolves the drop locator in the same retry as the source, and both must come
from the same screen. `SwipeAsync(new SwipeOptions { Direction = ..., Momentum = ... })`
scrolls that node. Where each runs: the web engine has all of them; a device
engine has long press, hover, drag, and swipe (no secondary tap); the Windows
engine has them all; the document engine has swipe only and fails the rest with
`UNSUPPORTED_CAPABILITY`. An engine declares the action kinds it takes, and the
screen refuses another before it resolves the node.

`TapAsync(new TapOptions { Count = 3 })` (also `ClickAsync`) taps the node that many times in a row, a triple click in a browser.
`Count` is a positive integer, 1 by default; 2 is `DoubleTapAsync()`, and 3 or more needs an engine that declares the
`multiTap` action (the web and mobile engines do; the mobile engine refuses more than 200 with `UNSUPPORTED_CAPABILITY`, and a device
presses at its own cadence, which a gesture detector may not read as one multi-tap). A `Count` that is not positive is
`INVALID_ARGUMENT`, and one above 1 with a `Position` is `INVALID_ARGUMENT` too.

`TapAsync(new TapOptions { Position = new Point(x, y) })` taps that point of the
node's box (from its top left corner, inside its border), once the node is in view with a box;
modifiers with a position are `INVALID_ARGUMENT`. `Screen.TapAtAsync(new Point(x, y))`
taps a viewport point with no locator, `Screen.DoubleTapAtAsync(new Point(x, y))`
double-taps one (the web engine sends a primary-button double click), and `Screen.SwipeAsync(new SwipePathOptions
{ From = ..., To = ..., Duration = ... })` drags between two viewport points
(`Duration` is a whole number of milliseconds from 16 through 10000). A point must
be finite and not negative. Viewport points also take percentages from `"0%"` through
`"100%"`, rounded to the nearest pixel: `new ScreenPoint("50%", "50%")`, or one of each,
`new ScreenPoint("50%", 120)`; a percentage reads the screen once for its size, and a
string that is not a percentage is `INVALID_ARGUMENT`. `TapAsync(new TapOptions { Position = ... })`
stays in pixels, so does the agent. Prefer a locator; a point moves with the layout. Only an engine that declares pointer actions takes a
point: the web engine does, and so does a device; the others fail with
`UNSUPPORTED_CAPABILITY`. The agent has no `long_press`, `right_click`, `hover`, or
`drag` tool yet.

`SetInputFilesAsync(path)` (or a list of paths, or one or a list of
`new FilePayload(name, mimeType, buffer)` with the bytes in memory) sets the files of exactly one
file input, or of a control that opens a file chooser, such as a button over a
hidden input. Paths and payloads cannot be mixed, as the types keep them apart. The
combined payload size must be less than 50 MiB, or it is `INVALID_ARGUMENT`
(`Cannot set buffer larger than 50Mb, please write it to a file and pass its path instead.`); a blank
`Name` is `INVALID_ARGUMENT` too. An engine that does not declare `setInputFilePayloads` refuses payloads
with `UNSUPPORTED_CAPABILITY`. A locator refuses a hidden node, so target the button or label
over a hidden input, not the input. Paths resolve from the project root; an
empty list clears the input and fires `input` and `change`, and a blank
entry is an `ArgumentException` (upstream's `INVALID_ARGUMENT`). On the web, a path that
does not exist, or sits under a directory the runner cannot search, is `INVALID_ARGUMENT`
before anything is clicked (the check is reachability, not read permission). A file input,
or a node inside a `<label>` for one, takes the files directly with no click.
Anything else is clicked with the file chooser already awaited, and the chooser
takes the files. A control that opens no chooser fails with `ACTION_FAILED`
(`opened no file chooser`), as every engine failure of a locator action does;
its `InnerException` is the engine's `NOT_ACTIONABLE`. Only the web engine has it: the others refuse it with
`UNSUPPORTED_CAPABILITY`. The agent has no `upload` tool yet.

### Reads

`TextContentAsync`, `InputValueAsync`, `GetAttributeAsync`,
`IsEnabledAsync`, `IsDisabledAsync`, `IsCheckedAsync`, and `BoundingBoxAsync`
wait for one match until the action timeout, then read once with no retry for
a value. A node that never arrives fails with `LOCATOR_NOT_FOUND` after the action
timeout. `IsVisibleAsync`, `IsHiddenAsync`, `CountAsync`, `AllAsync`, and
`AllTextContentsAsync` answer from the current screen at once. Use a matcher
when a value has to settle.
`WaitForAsync` waits for a state (`attached`, `detached`, `visible`,
`hidden`). `BoundingBoxAsync` is `null` for a node with no layout box
(`display: none` on it or an ancestor, or any `display: contents`
element). `InputValueAsync` on a checkbox or
radio reads its value attribute, `on` when it has none, whatever its checked
state; `IsCheckedAsync` reads the state. `IsCheckedAsync` on a node that cannot
be checked (a button), and `InputValueAsync` on one that holds no value (a
heading), throw `ACTION_FAILED`, so query checkable controls by role.

`TextContentAsync` is `null` for a node with no text and never falls back to
the node's name: an image reads `null`, not its alt text. `InputValueAsync`
returns a `string`, `""` for a field with no value (a cleared field), never
`null`. A node that holds no value at all, such as a heading, throws instead.

Text reads (`ToHaveTextAsync`, `ToContainTextAsync`, `TextContentAsync`, and
`AllTextContentsAsync`) read the node's rendered text, whitespace
collapsed. On the web that is what `innerText` reads, not the label:
`<output aria-label="Remaining">1 remaining</output>` reads `1 remaining`,
and an icon-only button reads `""`. A hidden node reads its DOM text, so a
text query still finds a `visibility: hidden` copy. Check a label with
`ToHaveAccessibleNameAsync`.

`ToHaveRoleAsync(role)` waits for the one match to have that role, compared as
a string. only the exact `img` reads as `image`, as `GetByRole` reads it; any other string,
`IMG` included, is compared as written, and an empty one is `INVALID_ARGUMENT`. It compares the
role `Screen.TreeAsync()` shows, so a `switch` is not a `checkbox`, and on the
web an editing host (`contenteditable`) reads as `textbox`. A node the tree
shows without a role fails it with `observed: no role`. It answers on a
secure field, whose role is never withheld.

`Screen.TreeAsync()` returns the whole screen as the agent reads it (one node
per line, children indented, secrets redacted) and `Screen.DebugAsync()` prints
it to the test's output. Use them to write a locator. Both read once without
waiting, and take an `ActionOptions` with a `Timeout`. A locator does not have
them.

`Screen.PauseAsync()` pauses the test at that line. Call it on the screen with
`await`: a frame's screen throws `INVALID_ARGUMENT`. Where nothing can resume a
test (a program on `Launch`, an `OpenHost`, or an MCP session), it returns right
away. Time spent paused does not count toward the test's timeout. In a run that holds, a call inside
another step (an `OnAction` hook, a project tool) or an `Expect.Poll` read is refused, and a pause the
test did not await fails it with `STEP_NOT_AWAITED` naming the code that returned first (`the test body`,
`the beforeEach hook`, `the afterEach hook`, `the hosted session`).

`Screen.ScrollUntilVisibleAsync(locator, new ScrollUntilVisibleOptions { Direction, Momentum, Timeout })`
scrolls the viewport (`ScrollDirection.Down`, `Momentum.Slow` by default) until the
locator resolves visibly, else `LOCATOR_NOT_FOUND`. On a locator
(`list.ScrollUntilVisibleAsync(row)`) it swipes that node, so a feed or a table that is
its own scroll container pages without the pointer hovering it first. `Timeout` is its own
default of 30 seconds, not the action timeout, and bounds the whole scroll, each swipe
included. `Momentum` is the stride of each step; a longer one must not carry the target
past the surface unseen, as a virtualized list does. The target must come from the same
screen, else `INVALID_LOCATOR`. It stops at the first match that is not hidden. On a device, a
node under the soft keyboard is hidden, so the scroll goes on (topic `mobile`, "Visibility").

`Screen.ScrollToAsync(ScrollEdge.Bottom)` scrolls the page or a list to an edge (`Top`, `Bottom`, `Left`,
`Right`), as far as it goes; `list.ScrollToAsync(edge)` scrolls that node or its nearest ancestor that scrolls
along the axis. `ActionOptions.Timeout` bounds the whole scroll, and it resolves at once when the container is at
the edge already or nothing scrolls. On the web the screen scrolls the page, else the pane under the viewport's
center, with the page's own scroll API, and follows content that grows at the end until a jump moves nothing. On a
device it drags from the middle of the node's box (locate the list, not a row), up to three drags between reads,
until a drag moves nothing, so a list with pull-to-refresh refreshes on `Top`, and a timer inside the container
keeps it dragging until the timeout. It fails `ACTION_FAILED` with the engine's reason when the timeout comes
first, `INVALID_ARGUMENT` for an unknown edge or a frame's screen, and `UNSUPPORTED_CAPABILITY` on the Windows
engine.

## Expect

`Expect.That(locator)` polls until the assertion timeout, reading at 0, 20, and
50 ms, then every 100 ms from the start:
`ToBeVisibleAsync`, `ToBeHiddenAsync`, `ToBeInViewportAsync`, `ToBeAttachedAsync`,
`ToBeEnabledAsync`, `ToBeDisabledAsync`, `ToBeEditableAsync`, `ToBeCheckedAsync`,
`ToContainTextAsync`, `ToHaveTextAsync`, `ToHaveValueAsync`,
`ToHaveCountAsync`, `ToHaveAttributeAsync`, and more. `.Not` inverts a
matcher. On a device, a node whose centre the soft keyboard covers is hidden for
`ToBeVisibleAsync` and `ToBeHiddenAsync`, except the keyboard's own keys,
suggestion bar, and dock, the focused field, and a container with a child still
showing (see the mobile reference).
- `ToBeInViewportAsync(ratio)` waits for part of the one match, or that share of its box
  (`ratio` from 0 to 1; 0 is any area; otherwise `INVALID_ARGUMENT`), to be inside the
  viewport, without scrolling. No match, a zero-size box, and (web) an element with no box
  such as `display: none` are outside, so `.Not` accepts zero matches. The web engine measures
  with the page's `IntersectionObserver`, as Playwright does, so scrolling and `overflow`
  ancestors and frames clip the box. A device reports bounds already clipped, so there
  `ratio > 0` fails with `UNSUPPORTED_CAPABILITY` once the node is on screen. It fails at once
  with `UNSUPPORTED_CAPABILITY`, `.Not` included, on an engine without `ViewportIntersectionAsync`
  or for a node whose bounds the engine cannot report.
- `ToBeEditableAsync()` waits for an enabled control that is not read-only;
  `ToBeEditableAsync(false)` waits for a disabled or read-only one. It reads the node's
  `readonly` state, which the web engine reports by Playwright's `getReadonly` rules: an
  `input`, `textarea`, or `select` by its `readonly` attribute alone, an element whose ARIA
  role takes `aria-readonly` by that attribute (any case), any other editing host and
  anything inside one never. A disabled node is not editable. Any other node, such as a
  button, has no read-only state and fails the matcher, negated too. On a device only
  Android reports it, for text fields and comboboxes; an iOS field fails the matcher.

- `Expect.That(Screen).ToHaveScreenshotAsync("home.png")` compares the whole
  screen, and `Expect.That(locator).ToHaveScreenshotAsync()` one element's box,
  against `<test file>-snapshots/<name>-<target>-<os>.png` (web only). Commit the
  files. The first run writes the file and fails: look at it, commit it, rerun. In
  CI (`CI` set) nothing is written into the project; the file is attached under
  `.e2e/results/` at `snapshots/<path>`. `E2E_UPDATE_SNAPSHOTS=1` writes missing
  and different files and passes, after an intended UI change. Options
  (`ScreenshotOptions`): `Threshold`, `MaxDiffPixels`, `MaxDiffPixelRatio`, `Mask`
  (locators painted over, for clocks and other changing content), `MaskColor`,
  `Timeout`. A failure writes `-expected`, `-actual`, and `-diff` images under
  `.e2e/results/`. Files are per target and OS (`-web-linux.png`): screenshots
  written on a Mac never serve Linux CI, so write CI's from a CI job with the
  update variable. After a secret fill the comparison still runs: the image
  masks secure fields only, so a secret the app shows anywhere else is in it. When the engine
  cannot prove every secure field on screen was masked, the call fails with
  `POLICY_DENIED` and keeps no screenshot. An engine that withheld the screenshot for that reason
  (iOS and Android with a secure field that has no bounds) fails it at once, not after the timeout. An engine that took no
  screenshot at all keeps the poll waiting, and the call fails as an assertion (`the engine returned no screenshot`).
  `App.ScreenshotAsync` fails the same way.
- `Expect.That(Browser).ToHaveURLAsync("/about")` (a string, or a `Regex`; `ignoreCase`)
  and `ToHaveTitleAsync("About page")` poll the URL and the title, `.Not`
  inverts them, and `Expect.Soft(Browser)` keeps their failures.
  `ToHaveClassAsync(locator, "card active")` waits for the locator's `class`
  attribute: a string is the whole normalized class list, a `Regex` is tested
  against it. A node that is not there yet keeps it polling and a node that
  never arrives fails it as an assertion (`observed: no node`); `class=""` is
  an empty list and no `class` attribute is not (`observed: no class
  attribute`); several matches fail at once with `LOCATOR_AMBIGUOUS`.
- `Expect.That(locator, message)`, `Expect.That(screen, message)`, and their `Expect.Soft` forms open each
  assertion failure with the message, `Not` included, and the `LOCATOR_AMBIGUOUS` error (also from
  `ToHaveScreenshotAsync`). `Expect.That(Browser)` takes none. `Expect.That(value, message)` is synchronous.
- `Expect.Soft(locator)` and `Expect.Soft(screen)` record a failure and let the test go on; the
  test fails at its end with every failure listed.
- An `Expect.Poll` the test body did not await is cancelled when the body returns, and the test fails with `STEP_NOT_AWAITED` at the line of the call, whether or not it would have passed. `await` every matcher.
- `Expect.Poll(read)` retries any value: `ToBeAsync`, `ToSatisfyAsync`, and,
  in `E2E.NUnit`, `ToMatchAsync` with any NUnit constraint. A read that
  throws, or a matcher that refuses the value's type (`Is.GreaterThan` on a
  string), is one failing sample, not the end of the poll. At the deadline
  the last one is reported. `PollOptions.Intervals` lists the pauses between reads (100, 250,
  500, then 1000 ms, the last repeating); `Interval` is deprecated, and both together is `INVALID_CONFIG`.
- `await Expect.That(block, message?).ToPassAsync(new PassOptions { Timeout, Intervals })` retries
  a block (an `Action`, `Func<Task>`, or `Func<CancellationToken, Task>`) until it stops throwing,
  for several assertions that must pass in the same try. Use hard `Expect` calls with a short
  `timeout` inside it. `Not.ToPassAsync()` passes on the first throw. `Expect.Soft(screen, block).ToPassAsync()`
  keeps a timeout as a soft failure. Defaults come from the `toPass` config key. With no `Timeout` the
  call needs a cancellation token that can end (`INVALID_CONFIG` otherwise). At the deadline it throws
  `ASSERTION_FAILED` with the last error as the inner exception. Await every call.
- A `Timeout` of `TimeSpan.Zero` on a locator action, `WaitForAsync`, an `Expect`
  matcher, `ToHaveScreenshotAsync`, `Screen.TreeAsync`, `Screen.DebugAsync`,
  `Browser.GotoAsync`, `WaitForResponseAsync`, `WaitForRequestAsync`, `WaitForDownloadAsync`, or
  `WaitForDownloadsAsync` sets no deadline of its own: the test timeout still bounds it.
  `Expect.Poll` with a zero `Timeout` polls until the cancellation token you pass ends,
  and without a token that can end it fails with `INVALID_CONFIG`. A negative `Timeout`
  is `INVALID_ARGUMENT` (`INVALID_CONFIG` from `Expect.Poll` and `ToPassAsync`, as is a bad
  `Intervals` list). `Agent` calls take a positive `Timeout` only: zero or less is
  `INVALID_ARGUMENT`.
- For plain values, `Expect.That(value, "what it checks")` runs the value matchers at once, with no
  retry: `ToBe`, `ToEqual`, `ToMatchObject`, `ToBeTruthy`, `ToBeFalsy`, `ToBeNull`, `ToBeUndefined`,
  `ToBeDefined`, `ToHaveLength`, `ToHaveProperty`, `ToContain`, `ToMatch`, `ToBeGreaterThan`,
  `ToBeGreaterThanOrEqual`, `ToBeLessThan`, `ToBeLessThanOrEqual`, `ToBeCloseTo`, and `ToMatchSchema`, each with `.Not`.
  The message opens every failure. `Expect.Any(typeof(int))`, `Anything()`, `ObjectContaining`,
  `ArrayContaining`, `StringContaining`, and `StringMatching` stand in for a value inside
  `ToEqual`, `ToMatchObject`, `ToContain`, and `ToHaveProperty`. `Expect.Soft(Screen, value)` keeps a
  failure. `Expect.Undefined` stands for `undefined` (`null` is only `null`, and `ToBeDefined` accepts `null`). The framework's
  asserts (`Assert.That`) still work.
- There is no Standard Schema in .NET, so `ToMatchSchema` takes a `StandardSchemaV1<TOutput>` delegate: `value => ...` returns `StandardSchemaV1Result<TOutput>.Success(output)` or `.Failure(issues)`, each `StandardSchemaV1Issue` with its path. Adapt your schema library in a lambda. `Expect.That<object?>(body).ToMatchSchema(usersSchema)` returns the typed output and fails listing every issue by its path. It is synchronous, so an async schema cannot be passed. `Expect.Soft(screen, value).ToMatchSchema(schema)` keeps a failure and returns `default` (`0` for an int) after it, where upstream returns `undefined`.

## Sign-in

There is no `test.setup` or saved `session`. Sign in inside each test, or in
the framework's per-test setup (NUnit `[SetUp]`, xUnit v3 `InitializeAsync`),
with `Screen` actions or `Agent.ActAsync`; both fill a `Secret`:

```csharp
var admin = Credentials.User("admin");
await App.OpenAsync("/login");
await Screen.GetByLabel("Email").FillAsync(admin.Username);
await Screen.GetByLabel("Password").FillAsync(admin.Password);
await Screen.GetByRole("button", "Sign in").ClickAsync();
```

A password field's value never reaches the model, unless a page script hides
the field from e2e (see "Redaction" in the security reference). A text, value,
or attribute expectation on a secure field fails with `POLICY_DENIED`: assert
the outcome, not the value.

A secret from `Secrets.Get(name)` is a generic one: the agent fills it into
any editable field, a rich-text editor included. A `Credentials.User` password
is a password secret and needs a password field. Redaction replaces a secret
with `<secret:name>` in these places:

- A failed matcher quotes the text it read from the page. A secret the page
  echoed, even upper-cased by CSS or folded onto one line, shows as its
  marker. The attempt knows every secret a `FillAsync` filled, so this holds
  for a secret that never went through `ActAsync`. The same holds for the
  messages of `Expect.That(Browser)`, a `ToHaveScreenshotAsync` failure, a
  `WaitForAsync` timeout, and the messages of the exceptions under any of
  them (`InnerException`).
- The aggregate of soft failures redacts its text when the test ends, so a
  secret the attempt learned after a failure was kept is covered.
- An `Expect.Poll` failure redacts the secrets of every live session in the
  process, since a poll has no session of its own.
- A name or text the engine cuts at its limit (256 and 512 characters) can end
  with the start of a secret. When the cut part is the first 8 or more
  characters of a secret (half of one shorter than 16), it shows the marker.
- Screenshots, the model's and the artifacts', mask secure fields only, after
  a secret fill too. A generic secret in a plain input, or a value the app
  shows anywhere else (a show-password toggle, a toast), is in the image and
  reaches the model provider: use test credentials.
- `new WebBasicAuth(username, secret)` in `WebEngineOptions.BasicAuth`: the
  attempt redacts the password and the `Authorization` credential the
  browser sends with it. This is text only. Screenshots stay on, and the agent
  cannot fill the value by name. A plain string password is not redacted.

## Start near the screen under test

A test pays for every step before the screen it checks, on every run and on
every rerun while you fix it. A wrong locator at the end of a long flow
reruns the whole flow, then waits out the action timeout. Begin each test as
close to its screen as the app allows:

- Sign in outside the flow, not through the sign-in form of every test. Save
  the sign-in once as a session (`[E2ESetup]` and `[E2ESession]`, topics
  `writing-tests-nunit` and `writing-tests-xunit`). Otherwise, or to seed data, do it in the
  framework's per-test setup
  (NUnit `[SetUp]`, which runs after the base class's own, so `Browser` is ready; xUnit v3 `InitializeAsync`, which calls
  `await base.InitializeAsync()` first, since the base starts the session):
  request the app's sign-in
  API with an `HttpClient`, then pass its cookies to `Browser.SetCookiesAsync`. To call the API as a user the test
  signed in through the UI, send `Browser.CookiesAsync(url)` in the `Cookie` header: it returns only the cookies the
  browser would send to that URL, so another site's, such as a sign-in provider's, stay out. Click through the form only in the
  test that covers it ("Sign-in").
- Open the screen by its path, `App.OpenAsync("/projects/42/settings")`, not
  the home page and three taps. `navigate` over MCP takes the same path.
- Create the data the screen needs through the app's API in that setup,
  under a run-unique name (`Guid.NewGuid()`).
  Click through a creation flow only in the test that covers it. When the
  app signs in with a cookie, the same `HttpClient` signs the setup's
  requests in. Check the response in the setup: a failed seed then fails
  there, not at the action timeout on a locator. Delete the data in the
  teardown (NUnit `[TearDown]`, xUnit v3 `DisposeAsync`, which cleans up
  before it calls the base), which runs after a failure too. A `Secret` from
  `Credentials.User` cannot go into a request body: read the account for the
  API from your own variables.

```csharp
private sealed record Project(string Id); // the shape of the API response

private HttpClient? _api;
private string _projectId = "";

[SetUp]
public async Task CreateProject()
{
    // A Secret cannot go into a request body, so a setup that calls the API
    // reads its own test account, such as these variables.
    var email = Environment.GetEnvironmentVariable("APP_ADMIN_EMAIL");
    var password = Environment.GetEnvironmentVariable("APP_ADMIN_PASSWORD");
    var jar = new CookieContainer();
    _api = new HttpClient(new HttpClientHandler { CookieContainer = jar }) { BaseAddress = new Uri(App.BaseUrl!) };
    (await _api.PostAsJsonAsync("/api/sign-in", new { email, password })).EnsureSuccessStatusCode();
    await Browser.SetCookiesAsync(jar.GetAllCookies().Select(c => new BrowserCookie { Name = c.Name, Value = c.Value, Url = App.BaseUrl }).ToList());
    var created = await _api.PostAsJsonAsync("/api/projects", new { name = $"Project {Guid.NewGuid()}" });
    created.EnsureSuccessStatusCode(); // a failed seed fails here
    _projectId = (await created.Content.ReadFromJsonAsync<Project>())!.Id;
}

[TearDown]
public async Task DeleteProject()
{
    // Runs after a failure too, and after a SetUp that failed partway.
    if (_api is not null && _projectId.Length > 0)
    {
        var deleted = await _api.DeleteAsync($"/api/projects/{_projectId}");
        if (!deleted.IsSuccessStatusCode && deleted.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            deleted.EnsureSuccessStatusCode();
        }
    }

    _api?.Dispose();
}

[Test]
public async Task Renames_a_project()
{
    await App.OpenAsync($"/projects/{_projectId}/settings"); // straight to the screen under test

    await Agent.ActAsync("rename the project to {name} and save", new ActOptions
    {
        Params = new Dictionary<string, object?> { ["name"] = "Atlas" },
    });
    await Screen.GetByRole("status").Filter(hasText: new Regex(@"^Saved$")).WaitForAsync();
    await App.OpenAsync($"/projects/{_projectId}/settings"); // reopened: the promise is that it is saved

    await Expect.That(Screen.GetByLabel("Name")).ToHaveValueAsync("Atlas");
}
```

On a device there are no cookies and `App.OpenAsync()` takes no path, unless the
target declares `app.linkBase` (`"linkBase": "myapp://"` makes
`App.OpenAsync("/projects/42")` open `myapp://projects/42`):
`Device.OpenLinkAsync("myapp://projects/42")` opens a route, or a sign-in
link, in the app. The step report shows the link without its userinfo, query, or fragment, where a magic link's token rides (`<link>` when it does not parse).

Check the create and delete statuses in the setup and the teardown, as the
example does.

Grow a test that spans several screens one screen at a time when you have
not checked its steps live: write up to the next screen you have not seen
pass, end it with a temporary check of that screen, run it, then replace
the check with the next part. The finished test keeps only its outcome
checks, in the assert block. A passing locator step takes milliseconds,
and an `ActAsync` that a later check confirmed replays from the cache with
no model call. The cache keeps those entries when a later line fails, so
each run pays model calls only for its new part. That holds for a first
attempt with a read-write cache: a retry never replays, and in CI the cache
is read-only by default, so grow the test outside CI and without an NUnit `[Retry]` (cache modes: topic `running`).
Keep the title and the goals already written as they are while the test
grows: a cache entry belongs to the test's file and title path and the
goal's instruction and params. A setup's run-unique value in a goal's
`Params` goes in as `Values.Unique(...)` (topic `agent`), or every run
misses.

## The browser fixture

`Browser` works with `WebEngine` only; the document engine answers
`UNSUPPORTED_CAPABILITY`.

- `GotoAsync(url, new GotoOptions { WaitUntil, Timeout })`, `ReloadAsync`, `BackAsync`, `ForwardAsync`, `UrlAsync`, `TitleAsync`,
  `WaitForURLAsync`, `EvaluateAsync<T>`, `SetViewportAsync`, and the raw
  keyboard and mouse. `EvaluateAsync<T>` returns plain JSON only: a class
  instance (a `DOMRect`, an element) is `INVALID_ARGUMENT`, so return
  `rect.toJSON()` or the fields you need. It fails with `EVALUATE_FAILED` when
  the code threw or when the page replaced a built-in such as `Map`.
- `Locator("css or xpath")`: a locator from a raw CSS or XPath selector, for a
  control no role, label, or test-id query names. Not portable: a last resort.
  It matches any element in the document (`body`, `html`, a wrapper `div`), as
  Playwright does; an element the tree does not list is read when the locator
  resolves. A selector that
  captures with `*` before its last part (`*css=ul >> li`) fails with
  `INVALID_LOCATOR`; `Filter(has)` narrows instead.
- `FrameLocator("#payment")`: a `Screen` scoped to one iframe
  (`Browser.FrameLocator("#payment").GetByLabel("Card number")`); it keeps
  `Locator(css)` and `FrameLocator(css)` for nesting. Every `Screen` query
  answers from inside the frame, and only a frame locator reaches it: a plain
  query does not read the content of an iframe. The agent's tree shows a frame
  from another site (a hosted card field) as its `iframe` node with none of
  its content, so an agent step reaches it only through pixels: script steps
  there with `Browser.FrameLocator`, and judge its content with `Vision`. An
  `Agent.ActAsync` whose only change is in that frame records no replay. The
  frame is the one element the selector names: a read that does not wait (`CountAsync`, `AllAsync`, `IsVisibleAsync`,
  `ToBeHiddenAsync`, `ToHaveCountAsync`, `WaitForAsync` for `Detached` or
  `Hidden`) answers no match while it is missing, and an action polls for it
  until its timeout, then fails with `LOCATOR_NOT_FOUND`. Two frames match: `LOCATOR_AMBIGUOUS`.
  `Expect.That(frame).ToHaveScreenshotAsync` and `TreeAsync` refuse a frame
  with `INVALID_ARGUMENT`: call them on `Screen`, or on a locator inside it.
- Shadow DOM: `Screen` queries reach open shadow roots and closed roots attached with `attachShadow`, not declarative closed roots (`<template shadowrootmode="closed">`). The engine
  tracks the closed roots through a record its init script defines on the page, so a page that
  replaces the global `WeakMap` keeps them reachable, and a secure field inside one is still masked in screenshots.
- Inert content: the agent doesn't see content that takes no input, so it never aims at it. That is an element with the `inert` attribute and everything under it, and
  everything outside a modal `<dialog>` opened with `showModal()` or a `show-modal` command. With several modal dialogs open, only the one shown last stays; when e2e can't tell
  which one that is, it leaves out nothing. Locators still find inert content, and `ToBeVisibleAsync` still passes, as in Playwright.
- `Keyboard.PressAsync(key)`, `Keyboard.TypeAsync(text)`,
  `Keyboard.DownAsync(key)`, `Keyboard.UpAsync(key)`, and `Mouse.*`: unfocused
  input. Only `DownAsync(key)` holds a key or modifier until `UpAsync(key)`;
  a second `DownAsync` of the same key sends a repeat event. `PressAsync(key)`
  presses and releases within the call. Prefer `PressAsync` and `FillAsync` on a
  locator.
- `WaitForResponseAsync(pattern | predicate, timeout?)`: returns a
  `PageResponse` with `Url`, `Status`, `Headers`, `Request`, `BodyAsync()`,
  `TextAsync()`, `JsonAsync<T>()`. Only the body readers are methods; `Headers`
  has lower-cased names. Resolves on headers unless a predicate reads the body.
  On the returned response, body readers wait for the body (up to the action
  timeout) and fail with `ACTION_FAILED` when it could not be read. An empty
  body gives an empty array from `BodyAsync()`, `""` from `TextAsync()`, and a
  failure from `JsonAsync<T>()`.
- `WaitForRequestAsync(pattern | predicate, timeout?)`: resolves when the page
  sends the request, before any response, with a `WebRequest` (`Url`, `Method`,
  `Headers`, `PostData`), the same type as `route.Request`.
- Start either wait before the triggering action, and await it after. Both work
  before the first `GotoAsync` or `App.OpenAsync`. Nothing matching in time is
  `ACTION_FAILED`, and a relative pattern with no base URL is
  `APP_URL_REQUIRED`. Traces label a predicate as `predicate`.
- A wait predicate is a `Func<PageResponse, bool>` or `Func<PageResponse, Task<bool>>`
  (`Func<WebRequest, ...>` for `WaitForRequestAsync`). It runs per event, async
  predicates can run concurrently, and the first match wins. Inside a
  `WaitForResponseAsync` predicate, body reads share the wait's timeout, so
  reading a streaming or unclosed body can wait until timeout; reads still
  pending on passed-over responses fail with `CANCELLED` when the wait settles.
  A thrown exception fails the step unchanged: an e2e error keeps its code,
  such as `ASSERTION_FAILED` from a failed assertion or `ACTION_FAILED` from an
  unreadable body.
- `RouteAsync(pattern, handler)`, `UnrouteAsync(pattern)`: intercept
  requests, newest route first; `pattern` is a URL pattern or a
  `new RouteMatcher { Method, Url | UrlRegex }` for one method.
  `route.Request` has `Url`, `Method`,
  `Headers`, `PostData`. `route.FetchAsync()` reads upstream without deciding
  the route and returns `Url`, `Status`, `Headers`, `BodyAsync()`, `TextAsync()`,
  `JsonAsync<T>()`; redirects are not followed. Hand binary bodies on with
  `BodyAsync()`, never `TextAsync()`. The handler calls exactly one of
  `FulfillAsync(new RouteFulfillResponse { Status, Headers, ContentType, Json | Body | BodyBytes | Path })`
  (`Body` is text, `BodyBytes` bytes; several cookies as one `\n`-joined
  `set-cookie`),
  `ContinueAsync(new RouteContinueOverrides { Url, Method, Headers, DropHeaders, PostData })`
  (straight to the network; `DropHeaders` names request headers to send
  without, the configured `Headers` still added on an allowed origin), `FallbackAsync()` (the route registered before
  it), or `AbortAsync()`. None or two fails the next step with
  `ACTION_FAILED`, a bad option with `INVALID_ARGUMENT`. `Path` is relative
  to the project root. An error the handler throws fails the next step; when
  no step follows, it fails the test at its end, after the teardown has run.
  A step the handler made fail, such as the `GotoAsync` whose request it left
  aborted, fails with the handler's error. A `GotoAsync` whose request a
  handler aborted on purpose fails with `ACTION_FAILED`.
- `UnrouteAllAsync()` removes every route; configured headers stay.
- A `RouteAsync`, `UnrouteAsync`, `WaitForResponseAsync`, or `WaitForRequestAsync` pattern is a
  glob string or a `Regex` matched against the full URL (`*` stays within
  one path segment, `**` crosses `/`, `?` is one character, `\` escapes the
  next one), or a `RouteMatcher` to match one HTTP method too,
  case-insensitively (`Method = "POST"` with `Url` as the glob or `UrlRegex`
  as the `Regex`). A method matches exactly that method: `GET` does not
  answer `HEAD`. A route narrowed to another method steps aside for the
  routes registered before it, then the network. `UnrouteAsync` removes
  routes registered with the same pattern, method included, rather than
  URL-matching them. A `RouteMatcher` with a missing or invalid part
  (no `Method`, no `Url`, a `Method` that is not an HTTP method, both `Url`
  and `UrlRegex`) is `INVALID_ARGUMENT`.
- `CookiesAsync(urls?)`: every cookie, or only those a request to one URL
  (`CookiesAsync(url)`) or a list would carry, by domain, path, and `Secure`;
  a relative URL resolves against the app URL, an empty list returns none.
- `SetCookiesAsync([...])`: a target is an http(s) URL,
  relative to the base URL, or a domain. `SetCookiesAsync` refuses a cookie
  URL that is not http(s), `about:blank` included, with `POLICY_DENIED`.
- `ClearCookiesAsync(new CookieFilter { Name, Domain, Path })`: remove the cookies
  matching every filter given. A string matches a field exactly, a `Regex` matches
  its text (Playwright runs it as JavaScript, so write `$` not `\z`). No options removes every
  cookie, `HttpOnly` ones included; local and session storage stay.
- `EmulateMediaAsync(new WebMedia { ColorScheme, ReducedMotion })`: what the
  page's `@media` rules and `matchMedia` read, for the rest of the test,
  through `App.RestartAsync` and `App.ClearStateAsync`. Call it before
  `App.OpenAsync` to load the page with it; a feature left out keeps its
  value. It sets every open tab and every tab opened after it. For every test,
  set `ColorScheme` and `ReducedMotion` on the engine.
- `Clock`: Playwright's `Page.Clock` for every tab: `InstallAsync(new BrowserClockInstallOptions { Time })`,
  `PauseAtAsync(time)`, `ResumeAsync()`, `RunForAsync(ticks)`, `FastForwardAsync(ticks)`,
  `SetFixedTimeAsync(time)`, `SetSystemTimeAsync(time)`. A `time` is epoch milliseconds, a date
  string, or a `DateTimeOffset`; `ticks` are milliseconds or `"ss"`, `"mm:ss"`, `"hh:mm:ss"`.
  Install and pause before `App.OpenAsync`, then `RunForAsync` to fire the page's timers; only
  the page's time moves, the test's timeouts and `Expect` polling keep real time. It survives
  navigation, `App.RestartAsync`, and `App.ClearStateAsync`; a CDP recovery after the test used
  it fails the attempt with `ENGINE_FAILURE`. A page timer that throws while the clock runs it
  is `EVALUATE_FAILED`; an argument Playwright would refuse, or `PauseAtAsync` before the
  clock's current time, is `INVALID_ARGUMENT`.
- `Tabs()`, `WaitForPopupAsync(trigger, timeout?)`, `NewTabAsync()`: a tab the app
  opens (`target="_blank"`, `window.open`) opens in the background: the active tab stays
  where it was. `WaitForPopupAsync(() => trigger)` makes the tab the trigger opens active
  and returns it. When the active tab closes, its opener is active again, or the tab
  active most recently when the opener is gone. `Tabs()` lists the open tabs in the order
  they opened; a `BrowserTab` has `UrlAsync`, `TitleAsync`, `IsActive`, `IsClosed`,
  `ActivateAsync`, and `CloseAsync`. `NewTabAsync()` opens a blank tab in the same
  context and makes it active. No tab in time is `ACTION_FAILED`.
- `AddInitScriptAsync(source | WebInitScript)`,
  `AddInitScriptAsync(WebInitScript.FromFunction(fn), arg)`: runs before the
  page's own scripts; call it before `App.OpenAsync`. `arg` is JSON.
- `OnDialogAsync(DialogHandler.Accept | DialogHandler.Dismiss | handler)`
  answers native `alert`, `confirm`, `prompt`, and `beforeunload` dialogs (see below).
- `WaitForDownloadAsync(trigger, timeout?)` runs the trigger and returns the
  file it downloads (see below).
- `WaitForDownloadsAsync(trigger, count, timeout?)` calls the trigger once and
  returns `count` of those files in start order (see below).

### Handle a dialog

An unhandled `alert`, `confirm`, `prompt`, or `beforeunload` dialog fails the
next step, and is dismissed. Register a handler first. It covers every tab
for the rest of the test, and the newest one answers. The call returns a
function that removes it:

```csharp
var off = await Browser.OnDialogAsync(DialogHandler.Accept);
await Screen.GetByRole("button", "Delete").TapAsync();
await off();
```

A function handler gets the `Dialog` (`Type`, `Message`) and must call
`AcceptAsync(text?)` or `DismissAsync()` before it returns. One that does
not, or that throws, fails the next step (`INVALID_STATE`, or
`ENGINE_FAILURE` for a throw that is not an e2e error), and its dialog is
dismissed. When no step follows, the test fails with it at its end:

```csharp
await Browser.OnDialogAsync(async dialog =>
{
    Assert.That(dialog.Type, Is.EqualTo(DialogType.Confirm));
    await dialog.AcceptAsync();
});
```

### Download a file

Before reporting, text downloads are scanned again with the final secrecy ledger,
including secrets filled later and ASCII secrets written as JSON Unicode escapes.

```csharp
var file = await Browser.WaitForDownloadAsync(() =>
    Screen.GetByRole("link", "Export CSV").TapAsync());
var csv = await File.ReadAllTextAsync(file.AbsolutePath);
```

The file is a `download` artifact of the step, saved under the attempt's
`downloads` folder with a counted name (`downloads/001-report.csv`). Read it
through `AbsolutePath`, which works with a hosted browser whose provider
serves `Downloads` too; `Path` is relative to the attempt's artifact
directory, not to the project. A text download (`text/*` or `application/json`) is rewritten in place when the session holds a secret value: every value, fragment, and encoded form of it becomes `<secret:name>`, a byte order mark stays, and the report records the artifact as `complete`; a binary file, a file that is not UTF-8 or contains NUL bytes (including BOM-less UTF-16), or a session with no secret leaves it as served and `incomplete`. `SuggestedFilename` is the name the page or
server gave it. A trigger that throws fails the call with its own error. One
that returns and starts no download within the timeout fails with `ACTION_FAILED`, naming how long it
waited and how long the trigger took. Without `Downloads` on the provider,
a download on a browser on another machine fails with `ENGINE_FAILURE`,
naming the provider. The timeout also bounds the file finishing and its save,
and a download still running at the timeout is canceled.

`WaitForDownloadsAsync(trigger, count, timeout?)` calls the trigger once and
returns `count` files in start order, each its own `download` artifact, under
the same timeout. A `count` below one fails with `INVALID_ARGUMENT` before the
trigger runs. When fewer start in time, the `ACTION_FAILED` error names both
counts; extra downloads are ignored.

```csharp
var files = await Browser.WaitForDownloadsAsync(
    () => Screen.GetByRole("button", "Export all").TapAsync(), count: 2);
```

### Run a script before the page

An init script runs in every document before the page's own scripts, in
every tab and frame: mock a browser API, seed `Math.random`, or set a flag
the app reads at boot. A script is JavaScript source (a string), a file
(`WebInitScript.FromPath`, relative to the project root), or a function's
JavaScript source (`WebInitScript.FromFunction`). A function cannot close
over test variables; pass a JSON `arg` instead. For every test, set
`InitScripts` on the engine:

```csharp
protected override IEngine CreateEngine() => new WebEngine(new WebEngineOptions
{
    InitScripts = [WebInitScript.FromPath("tests/init.js")],
});
```

For one test, before the page opens:

```csharp
await Browser.AddInitScriptAsync(WebInitScript.FromFunction("(value) => { Math.random = () => value; }"), 0.5);
await App.OpenAsync("/raffle");
```

A script added after the page opened runs from the next navigation or
reload. A configured file that cannot be read fails the test with
`INVALID_CONFIG`. A bad script or an argument passed with anything but a
function fails with `INVALID_ARGUMENT`, and a `Secret` in the argument with
`POLICY_DENIED`.

## Habits

- Prefer `Agent.ActAsync` for user flows when a model is configured; one
  behavior and one goal per test, and end the test with a check of the
  outcome ("Structure").
- One test class per feature, one behavior per test, arrange, act, assert
  ("Structure"). Name a repeated locator or value once; act on the second
  item shown in a list mutation, never the first.
- Prefer role, label, and text names over `Browser.Locator` or
  `GetByTestId`, which is the last resort.
- Prefer `Expect.That` with a locator when the check is exact. It needs no
  model and does not flake.
- Pass data in `Params`. Use `Values.Unique(...)` for fresh emails or names,
  and a `Secret` for passwords.
- Never add a sleep: wait for the condition with a matcher, a locator
  `WaitForAsync()`, or `Agent.WaitForAsync(...)`.
- Assert required literal fragments with a matcher such as
  `ToContainTextAsync`, and semantic outcomes where negation or phrasing can
  flip the meaning with `Agent.AssertAsync`.
