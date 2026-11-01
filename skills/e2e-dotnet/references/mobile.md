# Mobile tests

iOS and Android apps run on `MobileEngine` (project `E2E.Mobile`). The engine drives a simulator or emulator through [agent-device](https://github.com/callstack/agent-device). It needs Node 22.12 or newer. [`samples/MobileApp.E2ETests`](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/MobileApp.E2ETests) is a complete, working example with a MAUI app to drive. Read its README for the full steps.

## Contents

- The fixture
- A test
- Device controls
- Choosing a device
- Video
- A MAUI app with DevFlow
- Known limits

## The fixture

Install `E2E.Mobile.NUnit` (or `E2E.Mobile.XUnit.V3`) and derive from its `MobileAppTest`. The app comes from `e2e.config.json`. The `agents`, `cache`, and timeout settings come from the same file.

```json
{
  "targets": [
    {
      "platform": "ios",
      "app": {
        "bundleId": "dev.hardkoded.e2e.mobileapp",
        "appPath": "../MobileApp/bin/Debug/net10.0-ios/iossimulator-arm64/MobileApp.app"
      }
    }
  ]
}
```

```csharp
using E2E.Mobile.NUnit;

public sealed class GreetingTests : MobileAppTest
{
    [Test]
    public async Task Shows_the_greeting_form() =>
        await Expect.That(Screen.GetByText("Say hello")).ToBeVisibleAsync();
}
```

- A device target takes `app.bundleId` and `app.appPath`, not `app.url`. A web target takes only `app.url`. The `appPath` is relative to the folder of `e2e.config.json`. `app.launchArguments` (an array of strings) and `app.permissions` (permission name to `grant`, `deny`, or `reset`, for example `{ "camera": "grant" }`) ride every fresh launch of the app: the arguments reach the app process (iOS) or `am start` (Android), and the permissions are set before the launch, so no system prompt appears. A permission name the device does not know is `INVALID_CONFIG`, naming the nearest one. `app.identity` and `app.environment` key the replay cache (topic `setup`).
- `MobileAppTest` installs the build once per run (`Device.InstallAppAsync()`), launches the app fresh before each test, gives the agent `MobileTools`, and sets the step timeout to 90 seconds. A device target has no URL, so `App.OpenAsync()` takes no path: it launches the pinned app fresh, and `MobileAppTest` already does that before each test.
- To set a value in code, override `AppId`, `AppPath`, or `Platform`. Override `CreateMobileOptions(IDeviceProvider?)` to change the `MobileOptions`.
- `Device` is the device fixture of the test's session: `HomeAsync`, `OpenAppAsync`, `ForegroundAppAsync`, appearance, and the other controls. `Device.Locator(selector)` is a `Locator` from an agent-device selector (`"id=SW_VERSION_SPECIFIER"`, `"role=text label=\"26.2\""`), for nodes the `Screen` queries cannot name. It polls and is strict like any locator, and it needs the test's session (a test class derived from `MobileAppTest` has one). A selector agent-device cannot parse fails when the locator resolves, as `ENGINE_FAILURE`.
- `AttachesToRunningApp` is true with `E2E_MOBILE_PROVIDER=devflow`. Use it in `Assume.That` for a test that needs a relaunch.
- The environment wins over the config: `E2E_MOBILE_PLATFORM` (`ios` or `android`), `E2E_MOBILE_APP_PATH`, `E2E_MOBILE_DEVICE`, `E2E_MOBILE_PROVIDER` (`local`, `limrun`, or `devflow`), `E2E_DEVFLOW_PORT`, and `E2E_DEVFLOW_APP`.
- The base class adds no `[Category]`. Tag your tests, for example `[assembly: Category("Device")]`, so a run without a device can skip them.

## A test

`Screen` finds controls by test id (the MAUI `AutomationId`). The agent works out which field and button to use. Pair each agent step with a check that does not call the model. This is an excerpt of `GreetingTests.cs`.

```csharp
    [Test]
    public async Task The_agent_gets_a_greeting()
    {
        // The agent works out which field and button to use.
        await Agent.ActAsync("get the app to greet {name}", new ActOptions
        {
            Params = new Dictionary<string, object?> { ["name"] = "Grace" },
        });

        // Each agent step is paired with a check that does not call the model.
        await Expect.That(Screen.GetByTestId("greeting")).ToHaveTextAsync("Hello, Grace!");
    }
```

## Device controls

`Device.OpenAppAsync` launches or foregrounds an app. `new OpenAppOptions { Relaunch = true }` starts it fresh. This is an excerpt of `DeviceTests.cs`.

```csharp
    [Test]
    public async Task Keeps_the_count_after_the_app_goes_to_the_background()
    {
        // ...
        await Screen.GetByTestId("increment").TapAsync();
        await Expect.That(Screen.GetByTestId("counter")).ToHaveTextAsync("Count: 1");

        await Device.HomeAsync();
        await Device.OpenAppAsync(AppId);

        await Expect.That(Screen.GetByTestId("counter")).ToHaveTextAsync("Count: 1");
        Assert.That((await Device.ForegroundAppAsync()).BundleId, Is.EqualTo(AppId));
    }
```

The fixture also has `SetAppearanceAsync(Appearance.Dark)` and `InstallAppAsync()`.

`Device.UnstableRaw` gives the session name, device id and name, and an agent-device `IDeviceClient` (`Client`) bound to the test's session, for what `Device` lacks (`raw.Client.CallAsync("apps.list", raw.Selection.ToArgs(), ct)`). Client calls are not steps: no report, trace, replay, or secret redaction. Unstable: may change or go away in any release. After the worker disposes the session, reading it throws `INVALID_STATE`.

`Device.DismissKeyboardAsync()` presses the keyboard's own dismiss key where it has one (Android, iPad). An iPhone keyboard has none, so it drags a short way, horizontal first and vertical second, and stops as soon as the keyboard is gone. Each drag starts at the point nearest the centre of the screen that no control covers, inside a scroll view when the screen has one, so it never presses a button or toggles a checkbox. A screen with no clear point above the keyboard, or a keyboard that outlives both drags, fails with `UNSUPPORTED_CAPABILITY`. The message names the alternatives: the app's own Done or close control, or pressing Enter on the field.

## Choosing a device

**A local simulator or emulator.** Without `MobileOptions.Device`, the engine drives every booted device of the platform that no other agent-device session holds, as many as the run has slots. A device another tool holds through its own agent-device session is skipped. With every booted one held, `prepare` fails with `ENGINE_FAILURE` naming the holding sessions: boot another device, close a holding session, or name the device. A booted device nothing holds, such as one you use by hand, stays in the pool: name the device to keep the run off it. Set `E2E_MOBILE_PLATFORM=android` for an emulator. Boot exactly one device:

```bash
xcrun simctl boot "iPhone 17"
dotnet test samples/MobileApp.E2ETests
```

To pick a device, set `E2E_MOBILE_DEVICE` (the sample reads it into `MobileOptions.Device`) to a UDID. List UDIDs with `xcrun simctl list devices available`. A name works only when one simulator has it. Many machines have two simulators with one name, one per runtime. When no device is booted and the choice is ambiguous, the run fails with `AgentDeviceError` code `AMBIGUOUS_MATCH` and a message such as `10 devices match this request equally; select one explicitly.` The details list the devices. Boot one, or set the UDID.

**A hosted Limrun simulator.** Add the `E2E.Limrun` package and set `MobileOptions.Provider`:

```csharp
using E2E.Limrun;

var provider = new LimrunSimulators(new LimrunSimulatorsOptions { HardTimeout = "15m" });
```

The provider leases one Limrun iOS simulator or Android emulator when the engine prepares, installs `App.AppPath` before the tests start, and deletes the device when the run ends. Set `LIMRUN_API_KEY` in the environment that starts the tests. `LIMRUN_BASE_URL` reaches another API. `LimrunSimulatorsOptions` sets `Region`, `HardTimeout` (default `1h`, a Go duration such as `30m`), `Ios.Model`, `Android.Model`, and `Android.OsVersion` (default `15`). Set a short `HardTimeout`, so an interrupted run does not stay billed. The sample turns this on with `E2E_MOBILE_PROVIDER=limrun`:

```bash
export LIMRUN_API_KEY=...
E2E_MOBILE_PROVIDER=limrun dotnet test samples/MobileApp.E2ETests
```

The NUnit and xUnit adapters do not run the engine's prepare and finish hooks, so without help each test would lease its own billed device. `MobileAppTest` runs them once per run, so every test shares the one leased device, and it releases the device when the test process ends.

`e2e explore` and `e2e mcp` read the same variable. A target with `platform` `ios` or `android` and `E2E_MOBILE_PROVIDER=limrun` leases one Limrun device when the exploration or the MCP session starts, and releases it when that ends: the tool runs the engine's prepare hook as the one session opens and its finish hook as it closes, the hooks `MobileAppTest` runs once per test process. Each session leases its own device, a target admits one session at a time, and the lease is also released when a start fails, when the request is cancelled (`e2e explore` exits 130 on Ctrl+C, also while the lease is being taken), when an MCP session reaches its idle or time limit, when the server shuts down, and when the process exits. The release runs within the config's `cleanupTimeout` (30 seconds by default), and an interrupt does not cut it short. `LIMRUN_API_KEY` is redacted from what the tool prints or returns. The tool leaves out `E2E.Eas` (`EasSimulators`): set the provider in code for that. A failed release is reported, not lost: `e2e explore` still prints the exploration, with the failure as its error line when the run had no error of its own (status `Failed`, exit 3 or the verdict's code when that is higher; the findings stay; when the run already ended with an issue or an error, only the exit code shows the failed release), except after an interrupt: Ctrl+C exits 130 and prints nothing, so a failed release then goes unreported, and `close_session` shows a `Cleanup:` line. A release that outlives `cleanupTimeout` is reported as `CLEANUP_TIMEOUT`, and the device then bills until the provider's own limit (`HardTimeout`), so keep that short.

A provider whose service records its devices overrides `IDeviceProvider.RecordAsync(DeviceLease lease, ProviderRecordContext context, CancellationToken ct)`. When an attempt records video, the worker calls it in place of agent-device's screen recording, with the lease as it traveled to the worker: its `Id` and the other `DeviceLease` fields, never what a derived lease class kept on the object `AcquireAsync` returned. The context carries `RunId`, `TargetName`, `AttemptId`, and `Env`. It returns a `ProviderRecording`, as a browser provider's `RecordAsync` does: its `StartedAt`, and `StopAsync`, which either writes a file into the directory it gets and returns `new ProviderRecordingResult { File = "replay.mp4" }` (kept as `video/<file>`), or returns `{ Url, MediaType }` for a recording the service keeps, which the report links. The default returns null, and the engine records with agent-device. A `RecordAsync` that throws fails the start of the video as `ENGINE_FAILURE` naming the provider and the lease; a start that outlives its budget is cancelled, and the recording that comes up anyway is stopped when the session ends. A `StopAsync` that fails keeps the recording, so the end of the attempt stops it once more. A worker without a lease (an engine that did not run its prepare hook) records with agent-device.

A provider that drives its devices without agent-device implements `IDeviceClientSource` as well as `IDeviceProvider`. `MobileEngine` then asks it for the `IDeviceClient` of each session. The client finds its device through `DeviceConnection.Client`, which is the `DeviceLease.Client` the provider returned. This has no upstream counterpart.

## Video

With `video` on (see `references/debugging.md`), a local simulator or emulator attempt writes `video/video.mp4` under `.e2e/results/<result>/attempt-<n>/`: agent-device records the whole device screen, not one app, and its segment carries `StartedAt`. Taps show as a touch indicator. `new MobileOptions { VideoTouches = false }` records the plain screen. An iPhone video is built from screenshots at about 15 frames a second. A physical iPhone that `xcrun devicectl list devices` does not list cannot record. A hosted device provider that records its devices (`IDeviceProvider.RecordAsync`) records the attempt instead, and its segment is a file under `video/` or a link. The MAUI DevFlow engine cannot record.

### Record a transition

`App.StartRecordingAsync(new RecordingOptions { Name = "sheet" })` and `App.StopRecordingAsync()` record one span of the device screen as an `.mp4` under `recordings/`, and return the videos (`RecordedVideoFile.Path`, or `RecordedVideoLink.Url` for a hosted device's recording). `Locator.BoundingBoxAsync` gives an element's frame in points (physical pixels on Android). Start after `app.OpenAsync()`. A recording started after `Device.CloseAppAsync()` ends the device session when it stops, so open the app again after it. Taps show unless `MobileOptions.VideoTouches` is `false`. The test's `video` must be off: a start while it records is `UNSUPPORTED_CAPABILITY`.

## A MAUI app with DevFlow

A .NET MAUI app can be driven through the [MAUI DevFlow](https://github.com/dotnet/maui-labs/tree/main/src/DevFlow) agent instead of agent-device. The agent is an HTTP server inside the app, so no Node is needed, and every control reports its `AutomationId`. Add `Microsoft.Maui.DevFlow.Agent` to the app and register it in `MauiProgram.cs`:

```csharp
#if DEBUG
builder.AddMauiDevFlowAgent();
#endif
```

Then add the `E2E.Mobile.DevFlow` package to the tests and set `MobileOptions.Provider`:

```csharp
using E2E.Mobile.DevFlow;

var provider = new DevFlowAgents(new DevFlowAgentsOptions { AppName = "MobileApp" });
```

Launch the Debug build of the app on a simulator or emulator yourself, then run the tests. The provider finds the agent through the DevFlow broker when one runs (`maui devflow list` shows it), else on port 9223; `DevFlowAgentsOptions.Port` names a port outright. It waits up to `ConnectTimeout` (60 seconds) for the agent, checks that it runs the target's platform and app, and holds the agent's mutation lease while the run lasts. The sample turns this on with `E2E_MOBILE_PROVIDER=devflow`:

```bash
dotnet build samples/MobileApp -f net10.0-ios -r iossimulator-arm64 -t:Run
E2E_MOBILE_PROVIDER=devflow E2E_DEVFLOW_APP=MobileApp dotnet test samples/MobileApp.E2ETests
```

`e2e explore` and `e2e mcp` also run with `E2E_MOBILE_PROVIDER=devflow`. Their first launch of the pinned app attaches to the running app instead of relaunching it, and the session holds the agent's mutation lease until it closes.

Limits of the preview: the tests attach to the running app, so `Device.InstallAppAsync()`, `OpenAppAsync` with `Relaunch`, `OpenLinkAsync`, `HomeAsync`, `AlertAsync`, permissions, and orientation fail with `UNSUPPORTED_CAPABILITY`. Open the app without `Relaunch`. `SetAppearanceAsync` changes the app's theme, not the device's. The agent exists only in Debug builds. On Android, forward the agent's port first (`adb forward tcp:<port> tcp:<port>`), and `adb reverse tcp:19223 tcp:19223` before the app starts if a broker runs.

## Known limits

- Compose Multiplatform reports no placeholder on iOS: an empty field appends the text it draws to its label (`Name, Ada Lovelace`) over one `StaticText` child. For an empty (no value), editable, non-secure iOS field that reports no placeholder of its own, whose only child is a `StaticText` with no children, and whose label is a non-empty name, then `, `, then that child's text (`Name, Ada Lovelace`), that text is the field's `Placeholder`, the child is left out of the tree, and the name is the label before `, ` (a non-empty `ContentDescription` stays the name, and that label is then the `Text`). A field of any other shape keeps its label and children as reported. So `GetByPlaceholder("Ada Lovelace")` finds such a field and `GetByText("Ada Lovelace")` does not.
- `FillAsync` and `ClearAsync` read the field again before typing into it, and refuse anything that is not an editable field with `UNSUPPORTED_CAPABILITY`, a button or a text view included. An iOS `other`, the type of an element the platform did not classify, is typed into when it holds a value or only text, since a custom-drawn field (Compose Multiplatform, Flutter) can read that way; an `other` with no value and no children, such as a React Native `Pressable`, or one that holds a control, stays refused. Typing into a button or a text view that worked before now fails.
- On iOS, a plain React Native `View` has no children in the tree, so `Filter(has: ...)` on it and queries scoped to it match nothing, while `Filter(hasNot: ...)` keeps every such row, including one you meant to exclude. Scope to the screen's `ScrollView` (`Device.Locator("role=scroll-area")`), or give the children test ids.
- `Locator.PressAsync` takes `Enter`, `Space`, or one character, sent through the soft keyboard. A field that is not focused is tapped first, for `Enter` as for a character, so the keyboard is up. Other named keys and every modifier fail with `UNSUPPORTED_CAPABILITY`.
- Android needs `adb` from the Android SDK Platform-Tools on `PATH`, locally and for the Limrun tunnel.
- Building the sample iOS app needs `-p:ValidateXcodeVersion=false` when the installed Xcode is newer than the one the .NET iOS pack expects.
- Agent steps on a device are slower than in a browser. Raise `StepTimeout` to 90 seconds. The 30 second default timed out a two-field step.
- A ref belongs to the agent-device capture it came from, and every action pins it there (`@e4~s7`), so agent-device refuses a ref once a newer capture supersedes it. An action on a node that a newer capture replaced never acts on a different element: a test's action waits out `TransitionMilliseconds` for a control that arrived with the last action, and a node the fresh capture no longer lists is a retryable `NODE_STALE`, so the locator resolves again within its timeout. A direct `Device` call (`DragToAsync`, `LongPressAsync`) does not retry and surfaces it. Unmoved controls are acted on immediately only when keyboard visibility is unchanged: opening or closing the keyboard also uses the remaining `TransitionMilliseconds` budget. A `target_still_moving` refusal that reports `dispatched: no` is a retryable `NODE_STALE`. `FillAsync` and `ClearAsync` read the field again before typing: a field the fresh capture no longer lists, a changed input purpose or secure, disabled, or hidden state, or an incomplete capture is a retryable `NODE_STALE` before anything is typed; a node that cannot take text is `UNSUPPORTED_CAPABILITY`. `Device.DragToAsync` between nodes located in separate captures takes both refs from the newest one.
- An input that outlives its timeout. A tap, fill, key, swipe, or `Back` gets the `actionTimeout` (30 seconds by default). If the device doesn't answer in time, the step fails with `ACTION_MAY_HAVE_COMMITTED` and isn't retried: the input may have landed. An input the timeout ran out before (its target was still settling) isn't sent and fails with `OPERATION_TIMEOUT`. A long press that holds at least as long as the time left is refused before it's sent, as `NOT_ACTIONABLE`: raise the `actionTimeout`. A locate (the snapshot a locator or `Expect` takes), an observation, and a screenshot run within the timeout of the call that takes them, so a device that stops answering fails the step with `ACTION_FAILED` (the engine's `OPERATION_TIMEOUT` as its `InnerException`) instead of holding it until the test timeout. `WaitForAsync`, `Expect`, and `ToHaveScreenshotAsync` give their last read extra time, up to their slowest read so far and within the test timeout, so a screen that shows up during it still passes. An element that never shows up still fails as a locator timeout. A command a timeout or a cancel abandoned keeps running on the device, so the next attempt on that device waits for it to settle before it starts (within the launch timeout, which fails the start as cancelled when it runs out).
- Device tests need a device. Keep them out of CI, for example with the `RealModel` category (`dotnet test --filter "TestCategory!=RealModel"`).
- Android can report `Android snapshot helper returned insufficient foreground app content`, and iOS can report that the app is not running: agent-device could not read the screen yet, often an app still loading after a cold start. On iOS this only counts until the first readable screen after `OpenAppAsync`, `RestartAsync`, or `OpenLinkAsync`, as on a physical iPhone that is still launching the app; an app that exits later fails at once. A waiting locator action or `Expect` waits through it, as for an element not there yet. If the screen is still unreadable when the wait runs out, the step fails with this message. Immediate reads such as `CountAsync` and `IsVisibleAsync` see no matches instead, so `Not.ToBeVisibleAsync` can pass while the screen is unreadable, including on an app that crashed before it rendered. An observation, such as the agent's next turn, fails on it at once.
