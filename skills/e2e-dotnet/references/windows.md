# Windows app tests

Windows desktop apps run on `WinAppEngine` (project `E2E.WinApp`). The engine drives the app through Windows UI Automation, with the library behind the `winapp ui` command of the [Windows App CLI](https://learn.microsoft.com/windows/apps/dev-tools/winapp-cli/ui-automation). It runs on Windows only, and needs no other tool installed. WinUI 3, WPF, WinForms, and Win32 apps all expose UI Automation.

## Contents

- The fixture
- A test
- Window controls
- The agent's tools
- `e2e explore` and `e2e mcp`
- Requirements
- Known limits

## The fixture

Install `E2E.WinApp.NUnit` (or `E2E.WinApp.XUnit.V3`) and derive from its `WinAppTest`. The app comes from `e2e.config.json`. The `agents`, `cache`, and timeout settings come from the same file.

```json
{
  "targets": [
    {
      "platform": "windows",
      "app": {
        "appPath": "../MyApp/bin/x64/Debug/net10.0-windows10.0.22621.0/MyApp.exe",
        "bundleId": "MyApp"
      }
    }
  ]
}
```

```csharp
using E2E.WinApp.NUnit;

public sealed class SettingsTests : WinAppTest
{
    [Test]
    public async Task Shows_the_settings_page() =>
        await Expect.That(Screen.GetByRole("button", new() { Name = "Save" })).ToBeVisibleAsync();
}
```

- A `windows` target takes `app.appPath` and `app.bundleId`, not `app.url`. `appPath` is the executable to launch, relative to the folder of `e2e.config.json`. `bundleId` is the app's process name: with `appPath` it is the name the agent's `open_app` tool knows the app by; alone, the engine attaches to the running app of that name and never ends it.
- `WinAppTest` launches the app fresh before each test and ends it after when the target names `app.appPath`; a target with `app.bundleId` alone attaches to the running app and leaves it running, so tests share its state, gives the agent `WinAppTools`, and sets the step timeout to 90 seconds. A desktop app has no URL, so `App.OpenAsync()` takes no path: it relaunches the app, and `WinAppTest` already does that before each test. `App.RestartAsync()` and `App.ClearStateAsync()` relaunch it too; the engine does not know where the app keeps its data, so a test that needs clean data removes it before the launch.
- To set a value in code, override `AppId` or `AppPath`. Override `CreateWinAppOptions()` to change the `WinAppOptions`: `LaunchArguments`, `LaunchTimeout`, and `Settle` (the wait after an injected input, 150 ms by default; `Settle = false` skips it).
- `Window` is the window fixture of the test's session: open and close the app, resize and maximize its window, answer a dialog.
- `E2E_WINAPP_APP_PATH` names the executable over the config, so one config serves a build in another place.
- The base class adds no `[Category]`. Tag your tests, for example `[assembly: Category("Windows")]`, so a run on another host can skip them.

## A test

`Screen` finds controls by test id, which is the control's `AutomationId` (`AutomationProperties.AutomationId` in XAML, `.AutomationId(...)` in Microsoft.UI.Reactor), by role, by name, and by text. The roles follow the browser vocabulary: a `Button` is `button`, a `ToggleSwitch` is `switch`, an `Edit` is `textbox`, a `PasswordBox` is a secure `textbox`, a `ListView` is `list` with `listitem`s, a `ComboBox` is `combobox`, a `ContentDialog`, flyout, or popup is `dialog`, and the app's window is `window`. A control type the map does not name keeps its UI Automation type, lower-cased, as its role.

```csharp
[Test]
public async Task The_agent_signs_in()
{
    await Agent.ActAsync("sign in as {user}", new ActOptions { Params = new() { ["user"] = Credentials.User("benchmark") } });
    await Expect.That(Screen.GetByTestId("success-message")).ToBeVisibleAsync();
}
```

The benchmark app (`apps/WinAppBenchmark`) has tests for every locator verb. This is an excerpt of `LoginFormTests.cs`.

```csharp
    [Test]
    public async Task Signs_in_and_logs_out()
    {
        await Screen.GetByTestId("email-input").FillAsync(_account.Username);
        await Screen.GetByTestId("password-input").FillAsync(_account.Password);
        await Screen.GetByTestId("login-button").TapAsync();
        await Expect.That(Screen.GetByTestId("success-message")).ToHaveTextAsync("Logged in successfully");
        // ...
    }
```

Every action goes through the control's own UI Automation pattern when it has one, and through the keyboard and mouse otherwise:

| Action | What the engine does |
| --- | --- |
| `TapAsync` | The control's pattern: invoke, select, toggle, or expand. A control without one gets a click on its middle. |
| `DoubleTapAsync` | A double click on the control's middle. |
| `FillAsync`, `ClearAsync` | The value pattern. A control that refuses it (a `RichEditBox`) gets focus, `Ctrl+A`, and the text typed. |
| `PressAsync` | Focus, then the key: `Enter`, `Escape`, `Tab`, arrows, `F1` to `F12`, `Control+`, `Shift+`, `Alt+`, `Meta+` (the Windows key) and `ControlOrMeta+` (Control). One character is typed as itself. |
| `PressSequentiallyAsync` | Focus, then the text typed. |
| `SelectOptionAsync` | Expands a combo box, reads the options, and selects the one with that name, value, or automation id. A list, tab list, or radio group is read as it is. |
| `CheckAsync`, `UncheckAsync` | The toggle pattern. A radio button is selected; it cannot be unchecked. Nothing is sent when the state already matches. |
| `FocusAsync`, `ScrollIntoViewAsync` | The focus and scroll-item patterns. |
| `SwipeAsync` on a node | Up and down turn the mouse wheel over the control, a few notches at a time. Left and right use its scroll pattern, as does any swipe when the desktop cannot take input; the pattern steps by a share of the whole extent. On the screen, the largest scrollable control is swiped the same way, else the mouse wheel turns over the window for up and down. A left or right swipe with no scrollable control is `NOT_ACTIONABLE`. |
| `ToHaveScreenshotAsync` | A capture of the main window with every secure field painted black. |

The session also offers `HoverAsync`, `LongPressAsync`, `DragToAsync`, `SecondaryTapAsync` (a right click), and `TypeTextAsync` on `WindowSession`, which `WinAppEngine.ActiveSessions` lists.

## Window controls

`Window` is a fixture, not an agent tool. A test calls it to arrange or assert window state. Every method takes an optional `CancellationToken`.

| Member | What it does |
| --- | --- |
| `OpenAppAsync(app?, options?)` | Brings an app forward. Without a name, the app under test. `new OpenAppOptions { Relaunch = true }` ends it first; `LaunchArguments` apply to that launch alone. Another name is a running app, found by process name, window title, or process id. |
| `CloseAppAsync()` | Ends the app the session started, so the next `OpenAppAsync` launches it fresh. An attached app is left running. |
| `ForegroundAppAsync()` | The app's name and the title of its main window, as `ForegroundApp(Name, BundleId)`. |
| `ListWindowsAsync()` | The app's visible top-level windows, with titles and boxes. |
| `ResizeAsync(width, height)` | Resizes the main window to a logical size. The next observation reads the new layout. |
| `MaximizeAsync()`, `RestoreAsync()`, `MinimizeAsync()` | The window state. Keys and clicks need the window restored. |
| `AlertAsync(AlertAction.Accept \| Dismiss)` | Answers the open dialog, as the last observation saw it: accept presses its primary button (a `ContentDialog`'s `PrimaryButton`, else a button named OK, Yes, Confirm, Accept, Save, or Continue), dismiss presses its close or secondary button, else sends Escape. A dialog's listed buttons can be tapped by id too, and that tap replays from the trace cache. |

A test closes the app and opens it again, and the app starts from its home list. This is an excerpt of `DeviceFixtureTests.cs`.

```csharp
    [Test]
    public async Task CloseApp_then_openApp_relaunches_on_the_home_list()
    {
        await Window.CloseAppAsync();
        await Window.OpenAppAsync();
        Assert.That((await Window.ForegroundAppAsync()).Name, Is.EqualTo(AppName));
        // ...
    }
```

## The agent's tools

`WinAppTools.Create(engine)` is what `WinAppTest` hands the agent (`AgentTools`). Each tool is scoped to the `windows` platform.

- `open_app`: opens an app by name, with `relaunch` to restart the app under test fresh.
- `alert`: accepts or dismisses the open dialog.

Both are mutating, so the trace cache records them as replay gaps. The agent's tap, type, scroll, and screenshot are the engine's own actions and need no tool.

## `e2e explore` and `e2e mcp`

Both read the target's platform. A `windows` target launches the executable `app.appPath` names (or attaches to the running app `app.bundleId` names) before the first observation, the way a test does, and has no `navigate` tool. Run them on Windows, from the folder of `e2e.config.json`:

```bash
e2e explore "open the login form and sign in"
e2e mcp
```

On another host a `windows` target is `INVALID_CONFIG` before any session opens. A library caller of `Explore.ExploreAsync` with a `windows` target sets `ExploreOptions.Engine` to a `WinAppEngine`.

## Requirements

- Windows 10 or 11, .NET 10. The library is `Microsoft.Windows.SDK.BuildTools.WinApp.UIAutomation`, which the package brings; `winapp.exe` is not needed. The library ships Windows assets only, so a test project on plain `net10.0` adds `<AssetTargetFallback>$(AssetTargetFallback);net10.0-windows7.0</AssetTargetFallback>` and `NU1701` to `NoWarn` (or targets `net10.0-windows`); without it restore fails with `NU1202`.
- An unlocked, interactive desktop. Pattern actions (invoke, set value, toggle, select) work with the window anywhere; keys, clicks, drags, and the wheel go through `SendInput`, which reaches the window in the foreground only. A locked screen, a disconnected remote desktop, or a CI runner without a desktop session fails those with `NOT_ACTIONABLE`.
- WinUI 3 controls are windowless, so keys always go through `SendInput`; a `RichEditBox` takes no value pattern and is filled through the keyboard.
- The app is a process the engine can start: an unpackaged build (`WindowsPackageType None`) launches from its executable. A packaged (MSIX) app starts through a broker and exits at once, so name its process in `app.bundleId` and start it yourself, or launch it from the test with `Window.OpenAppAsync(name)`.

## Known limits

- `App.BackAsync` is `UNSUPPORTED_CAPABILITY`: a desktop app has no history the engine can see. Tap the app's own back button.
- `App.ClearStateAsync` relaunches the app and removes nothing.
- No video: `video` on a `windows` target is `UNSUPPORTED_ARTIFACT`.
- No tap modifiers: a pattern invoke holds no keys.
- A screenshot is the main window only. A flyout, menu, or combo box list that WinUI opens in its own popup window is in the observation, not in the image. Secure fields in the main window are covered; a secure field without a box withholds the image.
- `Selected` is read for radio buttons, tabs, list items, data items, and tree items (their selection item pattern) and for nothing else; a radio button that is selected is also checked.
- The agent's `open_app` opens apps, not URLs. There is no deep-link tool.
- The app's console output reaches the attempt's app log; a WinUI app writes little there.
