# macOS desktop app tests

Native macOS apps run on `DesktopEngine` (project `E2E.Desktop`), the port of upstream's `@e2e-dev/desktop`. The engine reads and drives the app through the macOS Accessibility tree, with a managed driver over the system frameworks that runs in the test process. Tests use the same `Screen`, `Expect`, and agent APIs as web tests. The engine runs on macOS 13 or later only. A config with a `macos` target still loads on Windows and Linux, and its other targets run there when you pick them with `Target`. A run that includes the `macos` target stops with `UNSUPPORTED_CAPABILITY` before any test starts.

## Contents

- Set up
- Point at your app
- A test
- The `Desktop` fixture
- What differs from the web
- Options
- `e2e explore` and `e2e mcp`

## Set up

Install `E2E.Desktop.NUnit` (or `E2E.Desktop.XUnit.V3` or `E2E.Desktop.TUnit`). On a Mac, the app that starts the tests needs two permissions in **System Settings > Privacy & Security**: your terminal, IDE, or CI agent needs **Accessibility**, to read and drive the app, and **Screen Recording**, for screenshots. `e2e doctor` checks the driver, both permissions, and the `appPath` build.

## Point at your app

```json
{
  "targets": [
    {
      "name": "mac",
      "platform": "macos",
      "app": { "appPath": "build/MyApp.app", "launchArguments": { "AppleLanguages": ["fr"] } }
    }
  ]
}
```

`app.appPath` is a built `.app` bundle, or `app.bundleId` an installed app's bundle id or name, such as `com.apple.calculator`. With both, the `appPath` build is launched, and its `CFBundleIdentifier` must be `bundleId`. `app.launchArguments` go to every launch. An object entry becomes `-name value`, as on iOS (`LaunchArgumentValues` in code), so `{ "AppleLanguages": ["fr"] }` opens the app in French. The launched app gets only a minimal environment (`HOME`, `USER`, `LOGNAME`, `SHELL`, `PATH`, `TMPDIR`, `LANG`, `LC_*`, `__CF_USER_TEXT_ENCODING`), so secrets, model keys, and CI tokens never leak into it. `app.url` and `app.permissions` are refused.

## A test

Derive from `DesktopTest`. By default `App.OpenAsync()` leaves the app in the background; a test launches the app itself, and every test ends with the app quit. Override `CreateDesktopOptions()` to return `new DesktopOptions { Headed = true }` for apps that need to come forward before they show a window (`--headed` also launches in the foreground, but only `Headed` turns on physical input).

```csharp
using E2E.Desktop.NUnit;

public sealed class SignInTests : DesktopTest
{
    [Test]
    public async Task Signs_in()
    {
        await App.OpenAsync();
        await Screen.GetByRole("textbox", new() { Name = "Username" }).FillAsync("ada");
        await Screen.GetByRole("button", new() { Name = "Sign in" }).TapAsync();
        await Expect.That(Screen.GetByTestId("status")).ToContainTextAsync("Signed in as ada");
    }
}
```

Roles use the names the web uses: `textbox`, `checkbox`, `combobox`, `button`. Static text is `text`, which is not a `GetByRole` role: find it with `GetByText`. A control's name is the one macOS accessibility gives it: its title, or its accessibility label. `GetByPlaceholder` is not supported: find a field with no label with `GetByRole("textbox", name)`. `GetByTestId` finds the control's `AXIdentifier`.

## The `Desktop` fixture

`Desktop.WindowsAsync()` lists the app's windows, `Desktop.UseWindowAsync(window)` picks the one `Screen` reads (until the app quits; no argument goes back to the focused one), `Desktop.PressAsync("ControlOrMeta+2")` presses a key into the app's window for an app with no control that takes keyboard focus, and `Desktop.CloseAppAsync()` quits it. Each call is a `desktop.<method>` step.

## What differs from the web

- **Input.** The app runs in the background. Actions use the Accessibility API and keys go into the app's own event queue, so your mouse and keyboard stay free. `DoubleTap`, `Hover`, and taps at a point need `Headed`, which brings the app forward and uses the real pointer and keyboard.
- **Keys.** Named keys, letters, digits, and ``, . / ; ' [ ] \ - = ` `` only. System shortcuts such as `ControlOrMeta+q` are refused: use `Desktop.CloseAppAsync()`. Without `Headed`, a `ControlOrMeta` shortcut reaches only the app's menu bar.
- **Select.** `SelectOptionAsync` matches by option label, not index. For an editable combo box, use `FillAsync`.
- **Windows.** `Screen` reads the window picked with `UseWindowAsync`, else the focused window, else the app's main window, else the first visible window. Sheets and open menus are included; window chrome is excluded. Minimized windows and rows scrolled out of their list read as hidden.
- **Reading.** A locked screen, a window deeper than `MaxDepth`, or one with more than the 8,000 elements a read takes makes locators keep retrying until timeout, and refuses screenshots of that window.
- **Not supported.** The menu bar, web content (use `WebEngine`), `App.ClearStateAsync`, `App.BackAsync`, video, tap modifiers, `GetByPlaceholder`, `ToBeInViewportAsync`, `BlurAsync`, `LongPressAsync`, `DragToAsync`, `SetInputFilesAsync`, and `SelectOptionAsync` by index. One worker per target.
- **Secrets.** A password field never exposes its value and is painted black in screenshots. A screenshot that cannot cover one is refused.

## Options

| `DesktopOptions` | Type | Default |
| --- | --- | --- |
| `Snapshot` | `DesktopSnapshotMode` (`Full` or `Interactive`) | `Full` |
| `Headed` | `bool` | `false` |
| `Settle` and `SettleMilliseconds` | `bool`, `int` | `true`, `150` |
| `MaxDepth` | `int`, 1 to 50 | `24` |

`Interactive` makes the agent's observations read only controls; locators and `Expect` read the whole window either way. `SettleMilliseconds` is how long the engine waits after an agent action before the agent reads the screen. `MaxDepth` is how many levels of a window a read goes down: raise it when locators on a deeply nested app time out on a read only in part.

## `e2e explore` and `e2e mcp`

A `macos` target works in `e2e explore` and `e2e mcp` on a Mac. The agent launches the app with `App.OpenAsync()` before its first observation, as a test does.
