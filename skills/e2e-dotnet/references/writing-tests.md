# Writing tests

## Contents

- Pick your framework
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

This topic covers what both share. A test body reads the same in either:

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

The agent does the flow; `Expect` pins what must be true after each goal,
and that check lets the replay cache rerun the step later. `Screen` actions
are for exact interactions and values. Every test starts from a fresh
browser context with no page open, so it calls `App.OpenAsync` first.
There is no `describe`, `test.setup`, `serial` group, or tag of e2e's own:
grouping, setup, and categories are the framework's.

## Fixtures

Members of `E2ETest` in both frameworks:

- `App`: `OpenAsync(path)`, `BackAsync`, `RestartAsync`, `ClearStateAsync`,
  `ScreenshotAsync(label)`, and `BaseUrl`. A path resolves against the target URL. `OpenAsync` and the
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
  else is in the image. It is `UNSUPPORTED_CAPABILITY` only on an engine that
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
- `Expect`: `Expect.That(locator)`, `Expect.That(Browser)`, `Expect.Soft(locator)`, `Expect.Poll(read)`.
- `Browser`: URL, title, cookies, viewport, raw keyboard and mouse, and
  responses (below).
- `Secrets.Get(name)`: a `Secret` from the config. `Credentials.User(name)`:
  an account from `E2E_USER_<NAME>_USERNAME` and `_PASSWORD`.

## Locators

- `GetByRole(role, name)`, `GetByText`, `GetByLabel`, `GetByPlaceholder`,
  `GetByTestId`, and `GetByDisplayValue`. Prefer a role with the accessible
  name the app renders.
- Text matches are **exact by default**. Pass `exact: false` for a
  case-insensitive substring, or a .NET `Regex`.
- `GetByText` and `GetByLabel` return the innermost match, so a container
  echoing its child does not count twice: `GetByText("$42.00")` finds the
  `<strong>` in `<p>Order total: <strong>$42.00</strong></p>`. Text inside a
  control's `<label>` answers with the control.
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
  `STRICT_MODE`; no match fails with `NOT_FOUND`.
- `Browser.Locator(css)` and `Browser.FrameLocator(css)` (see the browser
  fixture) reach a control a role, label, or text query cannot name, and an
  iframe. Use a selector only when those can't find the element.
- `Visible = true` (on `TextMatchOptions` or `RoleOptions`) drops nodes the
  page hides (a closed drawer) before the exactly-one rule. Visibility is
  what renders, as `ToBeVisibleAsync` reads it: `display: none`, a
  `visibility` other than `visible`, and content under
  `content-visibility: hidden` or a closed `details` are hidden. A child that
  sets `visibility: visible` under a hidden parent is visible, and a
  `display: contents` element is visible when something under it is. An
  `aria-hidden` node that paints is visible, as upstream, but role queries skip
  it. Unlike upstream, a box with no size reads as visible.
- On the web a `contenteditable` editing host, the root a TipTap, ProseMirror,
  Lexical, or Slate editor renders into, is a `textbox` for the agent and the
  locators. Its value is its text as rendered, spaces and newlines kept, so
  `ToHaveValueAsync` and `InputValueAsync` read it and `FillAsync` fills it;
  an empty editor reads `""`. It is named by its `aria-label`,
  `aria-placeholder`, or the `data-placeholder` its editor paints, and the
  blocks inside it are content, not textboxes. A native control or a drawn
  surface made editable keeps its own role. `GetByRole("textbox", name)` finds
  a bare host; upstream's finds it only with `role="textbox"`. Anchor a test
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
`UncheckAsync`, `ClearAsync`, `SelectOptionAsync` (a value or label),
`FocusAsync`, `SetInputFilesAsync`, and `ScrollIntoViewAsync`. Each waits up to
the action timeout for exactly one enabled, visible match.

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
`UNSUPPORTED_CAPABILITY`. The screen-level `TapAt` and `Swipe`, the tap
`position`, and the `{ label, value, index }` forms of `SelectOptionAsync` are not
ported yet, and the agent has no `long_press`, `right_click`, `hover`, or `drag`
tool yet.

`SetInputFilesAsync(path)` (or a list of paths) sets the files of exactly one
file input, or of a control that opens a file chooser, such as a button over a
hidden input. A locator refuses a hidden node, so target the button or label
over a hidden input, not the input. Paths resolve from the project root; an empty list or a blank
entry is an `ArgumentException` (upstream's `INVALID_ARGUMENT`). A file input,
or a node inside a `<label>` for one, takes the files directly with no click.
Anything else is clicked with the file chooser already awaited, and the chooser
takes the files. A control that opens no chooser fails with `NOT_ACTIONABLE`
(`opened no file chooser`). Upstream reports it as `ACTION_FAILED`, as it does
every engine failure of a locator action; the port's locator keeps the engine
code (#846). Only the web engine has it: the others refuse it with
`UNSUPPORTED_CAPABILITY`. The agent has no `upload` tool yet.

### Reads

`TextContentAsync`, `InputValueAsync`, `GetAttributeAsync`,
`IsEnabledAsync`, `IsDisabledAsync`, `IsCheckedAsync`, and `BoundingBoxAsync`
wait for one match until the action timeout, then read once with no retry for
a value. A node that never arrives fails with `NOT_FOUND` after the action
timeout. `IsVisibleAsync`, `IsHiddenAsync`, `CountAsync`, `AllAsync`, and
`AllTextContentsAsync` answer from the current screen at once. Use a matcher
when a value has to settle.
`WaitForAsync` waits for a state (`attached`, `detached`, `visible`,
`hidden`). `BoundingBoxAsync` is `null` for a node with no layout box
(`display: none` on it or an ancestor, or any `display: contents`
element). `InputValueAsync` on a checkbox or
radio reads its value attribute, `on` when it has none, whatever its checked
state; `IsCheckedAsync` reads the state.

`TextContentAsync` is `null` for a node with no text and never falls back to
the node's name: an image reads `null`, not its alt text. `InputValueAsync`
returns a `string`, `""` for a node with no value (a heading, a cleared
field), never `null`. A caller that tested it for null tests for `""` now.

Text reads (`ToHaveTextAsync`, `ToContainTextAsync`, `TextContentAsync`, and
`AllTextContentsAsync`) read the node's rendered text, whitespace
collapsed. On the web that is what `innerText` reads, not the label:
`<output aria-label="Remaining">1 remaining</output>` reads `1 remaining`,
and an icon-only button reads `""`. A hidden node reads its DOM text, so a
text query still finds a `visibility: hidden` copy. Check a label with
`ToHaveAccessibleNameAsync`.

`Screen.TreeAsync()` returns the whole screen as the agent reads it (one node
per line, children indented, secrets redacted) and `Screen.DebugAsync()` prints
it to the test's output. Use them to write a locator. Both read once without
waiting, and take an `ActionOptions` with a `Timeout`. A locator does not have
them.

`Screen.ScrollUntilVisibleAsync(locator, new ScrollUntilVisibleOptions { Direction, Momentum, Timeout })`
scrolls the viewport (`ScrollDirection.Down`, `Momentum.Slow` by default) until the
locator resolves visibly, else `LOCATOR_NOT_FOUND`. On a locator
(`list.ScrollUntilVisibleAsync(row)`) it swipes that node, so a feed or a table that is
its own scroll container pages without the pointer hovering it first. `Timeout` is its own
default of 30 seconds, not the action timeout, and bounds the whole scroll, each swipe
included. `Momentum` is the stride of each step; a longer one must not carry the target
past the surface unseen, as a virtualized list does. The target must come from the same
screen, else `INVALID_LOCATOR`.

## Expect

`Expect.That(locator)` polls until the assertion timeout:
`ToBeVisibleAsync`, `ToBeHiddenAsync`, `ToBeAttachedAsync`,
`ToBeEnabledAsync`, `ToBeDisabledAsync`, `ToBeCheckedAsync`,
`ToContainTextAsync`, `ToHaveTextAsync`, `ToHaveValueAsync`,
`ToHaveCountAsync`, `ToHaveAttributeAsync`, and more. `.Not` inverts a
matcher.

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
  `POLICY_DENIED` and keeps no screenshot.
- `Expect.That(Browser).ToHaveURLAsync("/about")` (a string, or a `Regex`; `ignoreCase`)
  and `ToHaveTitleAsync("About page")` poll the URL and the title, `.Not`
  inverts them, and `Expect.Soft(Browser)` keeps their failures.
  `ToHaveClassAsync(locator, "card active")` waits for the locator's `class`
  attribute: a string is the whole normalized class list, a `Regex` is tested
  against it. A node that is not there yet keeps it polling and a node that
  never arrives fails it as an assertion (`observed: no node`); `class=""` is
  an empty list and no `class` attribute is not (`observed: no class
  attribute`); several matches fail at once with `STRICT_MODE`.
- `Expect.Soft(locator)` records a failure and lets the test go on; the
  test fails at its end with every failure listed.
- `Expect.Poll(read)` retries any value: `ToBeAsync`, `ToSatisfyAsync`, and,
  in `E2E.NUnit`, `ToMatchAsync` with any NUnit constraint. A read that
  throws, or a matcher that refuses the value's type (`Is.GreaterThan` on a
  string), is one failing sample, not the end of the poll. At the deadline
  the last one is reported.
- For plain values, use the framework's asserts (`Assert.That`).

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

A password field's value never reaches the model, and a text, value, or
attribute expectation on it fails with `POLICY_DENIED`: assert the outcome,
not the value.

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
- `new WebBasicAuth(username, secret)` in `WebEngineOptions.BasicAuth`: the
  attempt redacts the password and the `Authorization` credential the
  browser sends with it. This is text only. Screenshots stay on, and the agent
  cannot fill the value by name. A plain string password is not redacted.

## Start near the screen under test

A test pays for every step before the screen it checks, on every run and on
every rerun while you fix it. A wrong locator at the end of a long flow
reruns the whole flow, then waits out the action timeout. Begin each test as
close to its screen as the app allows:

- Sign in outside the flow, not through the sign-in form of every test. The
  port has no saved `session`, so do it in the framework's per-test setup
  (NUnit `[SetUp]`, which runs after the base class's own, so `Browser` is ready; xUnit v3 `InitializeAsync`, which calls
  `await base.InitializeAsync()` first, since the base starts the session):
  request the app's sign-in
  API with an `HttpClient`, then pass its cookies to `Browser.SetCookiesAsync`. Click through the form only in the
  test that covers it ("Sign-in").
- Open the screen by its path, `App.OpenAsync("/projects/42/settings")`, not
  the home page and three taps. `navigate` over MCP takes the same path.
- Create the data the screen needs through the app's API in that setup.
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
        await _api.DeleteAsync($"/api/projects/{_projectId}");
    }

    _api?.Dispose();
}

[Test]
public async Task Renames_a_project()
{
    await App.OpenAsync($"/projects/{_projectId}/settings"); // straight to the screen under test
    await Screen.GetByLabel("Name").FillAsync("Atlas");
    await Screen.GetByRole("button", "Save").ClickAsync();
    await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("Saved");
}
```

On a device there are no cookies and `App.OpenAsync()` takes no path:
`Device.OpenLinkAsync("myapp://projects/42")` opens a route, or a sign-in
link, in the app.

Grow a test that spans several screens one screen at a time when you have
not checked its steps live: write up to the next screen you have not seen
pass, run it, then add the next. A passing locator step takes milliseconds,
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
  keyboard and mouse.
- `Locator("css or xpath")`: a locator from a raw CSS or XPath selector, for a
  control no role, label, or test-id query names. Not portable: a last resort.
  It reads the same tree as `Screen`, so an element the tree leaves out (no
  role, test id, or text of its own) matches nothing. A selector that
  captures with `*` before its last part (`*css=ul >> li`) fails with
  `INVALID_LOCATOR`; `Filter(has)` narrows instead.
- `FrameLocator("#payment")`: a `Screen` scoped to one iframe
  (`Browser.FrameLocator("#payment").GetByLabel("Card number")`); it keeps
  `Locator(css)` and `FrameLocator(css)` for nesting. Every `Screen` query
  answers from inside the frame. The frame is the one element the selector
  names: a read that does not wait (`CountAsync`, `AllAsync`, `IsVisibleAsync`,
  `ToBeHiddenAsync`, `ToHaveCountAsync`, `WaitForAsync` for `Detached` or
  `Hidden`) answers no match while it is missing, and an action polls for it
  until its timeout, then fails with `NOT_FOUND`. Two frames match: `STRICT_MODE`.
  `Expect.That(frame).ToHaveScreenshotAsync` and `TreeAsync` refuse a frame
  with `INVALID_ARGUMENT`: call them on `Screen`, or on a locator inside it.
- Shadow DOM: `Screen` queries reach open shadow roots and closed roots attached with `attachShadow`, not declarative closed roots (`<template shadowrootmode="closed">`). The engine
  tracks the closed roots through a record its init script defines on the page, so a page that
  replaces the global `WeakMap` keeps them reachable, and a secure field inside one is still masked in screenshots.
- `Keyboard.PressAsync(key)`, `Keyboard.TypeAsync(text)`,
  `Keyboard.DownAsync(key)`, `Keyboard.UpAsync(key)`, and `Mouse.*`: unfocused
  input. Only `DownAsync(key)` holds a key or modifier until `UpAsync(key)`;
  a second `DownAsync` of the same key sends a repeat event. `PressAsync(key)`
  presses and releases within the call. Prefer `PressAsync` and `FillAsync` on a
  locator.
- `WaitForResponseAsync(pattern, timeout?)` resolves once the headers
  arrive, with a `WebResponse` (`Url`, `Status`, `Headers`, `TextAsync()`,
  `JsonAsync<T>()`). `TextAsync` and `JsonAsync` wait for the body (up to
  the action timeout) and fail with `ACTION_FAILED` when it could not be
  read. Start it before the step that sends the request, and await it after.
  This includes the first `GotoAsync` or `App.OpenAsync`.
- `RouteAsync(pattern, handler)`, `UnrouteAsync(pattern)`: intercept
  requests, newest route first. `route.Request` has `Url`, `Method`,
  `Headers`, `PostData`. `route.FetchAsync()` reads upstream without deciding
  the route and returns `Url`, `Status`, `Headers`, `TextAsync()`,
  `JsonAsync<T>()`; redirects are not followed. The handler calls exactly one of
  `FulfillAsync(new RouteFulfillResponse { Status, Headers, ContentType, Json | Body | Path })`,
  `ContinueAsync(new RouteContinueOverrides { Url, Method, Headers, PostData })`
  (straight to the network), `FallbackAsync()` (the route registered before
  it), or `AbortAsync()`. None or two fails the next step with
  `ACTION_FAILED`, a bad option with `INVALID_ARGUMENT`. `Path` is relative
  to the project root. An error the handler throws fails the next step; when
  no step follows, it fails the test at its end, after the teardown has run.
- `UnrouteAllAsync()` removes every route; configured site headers stay.
- A `RouteAsync`, `UnrouteAsync`, or `WaitForResponseAsync` pattern is a
  glob string or a `Regex` matched against the full URL (`*` stays within
  one path segment, `**` crosses `/`, `?` is one character, `\` escapes the
  next one).
- `CookiesAsync()`, `SetCookiesAsync([...])`: a target is an http(s) URL,
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

- One goal per `ActAsync`, followed by a check of its outcome.
- Prefer `Expect.That` with a locator when the check is exact. It needs no
  model and does not flake.
- Pass data in `Params`. Use `Values.Unique(...)` for fresh emails or names,
  and a `Secret` for passwords.
- Never add a sleep: wait for the condition with a matcher.
