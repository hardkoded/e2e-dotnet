# Mobile tests

iOS and Android apps run on `MobileEngine` (project `E2E.Mobile`). The engine drives a simulator or emulator through [agent-device](https://github.com/callstack/agent-device). It needs Node 22.12 or newer. [`samples/MobileApp.E2ETests`](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/MobileApp.E2ETests) is a complete, working example with a MAUI app to drive. Read its README for the full steps.

## Contents

- The fixture
- A test
- Device controls
- Choosing a device
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

- A device target takes `app.bundleId` and `app.appPath`, not `app.url`. A web target takes only `app.url`. The `appPath` is relative to the folder of `e2e.config.json`.
- `MobileAppTest` installs the build once per run (`Device.InstallAppAsync()`), launches the app fresh before each test, gives the agent `MobileTools`, and sets the step timeout to 90 seconds. A device target has no URL, so a test does not call `App.OpenAsync()`.
- To set a value in code, override `AppId`, `AppPath`, or `Platform`. Override `CreateMobileOptions(IDeviceProvider?)` to change the `MobileOptions`.
- `Device` is the device fixture of the test's session: `HomeAsync`, `OpenAppAsync`, `ForegroundAppAsync`, appearance, and the other controls.
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

## Choosing a device

**A local simulator or emulator.** Without `MobileOptions.Device`, the engine drives the booted device of the platform. Set `E2E_MOBILE_PLATFORM=android` for an emulator. Boot exactly one device:

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

A provider that drives its devices without agent-device implements `IDeviceClientSource` as well as `IDeviceProvider`. `MobileEngine` then asks it for the `IDeviceClient` of each session. The client finds its device through `DeviceConnection.Client`, which is the `DeviceLease.Client` the provider returned. This has no upstream counterpart.

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

Limits of the preview: the tests attach to the running app, so `Device.InstallAppAsync()`, `OpenAppAsync` with `Relaunch`, `OpenLinkAsync`, `HomeAsync`, `AlertAsync`, permissions, and orientation fail with `UNSUPPORTED_CAPABILITY`. Open the app without `Relaunch`. `SetAppearanceAsync` changes the app's theme, not the device's. The agent exists only in Debug builds. On Android, forward the agent's port first (`adb forward tcp:<port> tcp:<port>`), and `adb reverse tcp:19223 tcp:19223` before the app starts if a broker runs.

## Known limits

- Keyboard actions such as `Locator.PressAsync("Enter")` fail with `UNSUPPORTED_CAPABILITY` on iOS, with the pinned agent-device. Tap an app button when the flow has one.
- Android needs `adb` from the Android SDK Platform-Tools on `PATH`, locally and for the Limrun tunnel.
- Building the sample iOS app needs `-p:ValidateXcodeVersion=false` when the installed Xcode is newer than the one the .NET iOS pack expects.
- Agent steps on a device are slower than in a browser. Raise `StepTimeout` to 90 seconds. The 30 second default timed out a two-field step.
- Device tests need a device. Keep them out of CI, for example with the `RealModel` category (`dotnet test --filter "TestCategory!=RealModel"`).
