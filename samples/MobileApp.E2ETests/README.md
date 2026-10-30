# Mobile app sample

A small .NET MAUI app ([`samples/MobileApp`](../MobileApp)) and the tests that drive it on a device with `MobileEngine`. It is the .NET counterpart of upstream's `examples/with-swiftui`: a name field with a Greet button, plus a counter.

The tests cover both ways to drive the app:

- `GreetingTests` finds controls by test id (the MAUI `AutomationId`) and taps and fills them. Three tests use no model. Two let the agent do the same steps, and check the result with a locator.
- `DeviceTests` uses the `Device` fixture: send the app home and bring it back, relaunch it, switch to dark mode, and read the foreground app.

All tests are in the `RealModel` and `Device` categories. They need a device, so `dotnet test E2E.slnx --filter "TestCategory!=RealModel"` skips them, and CI does too. To run the other real-model tests without these, use `--filter "TestCategory=RealModel&TestCategory!=Device"`.

## Prerequisites

- macOS with Xcode and an iOS simulator runtime, or an Android emulator (set `E2E_MOBILE_PLATFORM=android`).
- Node 22.12 or newer. `MobileEngine` starts [agent-device](https://github.com/callstack/agent-device) through `npx`, and agent-device drives the device. Check the setup once with `npx --yes agent-device@0.21.35 doctor`.
- The MAUI workload: `dotnet workload install maui`.
- A model login for the agent tests: `dotnet run --project src/E2E.Cli -- login github-copilot`, or an API key in `e2e.config.json`.

## Build the app

```bash
dotnet build samples/MobileApp -f net10.0-ios -r iossimulator-arm64
```

This writes `samples/MobileApp/bin/Debug/net10.0-ios/iossimulator-arm64/MobileApp.app`, the path the tests install by default. The Android path was not run. To try it, build `-f net10.0-android -t:SignAndroidPackage`, boot an emulator, and run the tests with `E2E_MOBILE_PLATFORM=android`. Also set `E2E_MOBILE_APP_PATH` to the `.apk`: `e2e.config.json` holds the iOS build path only.

The app is not in `E2E.slnx`. A machine without the MAUI workload could not build it, and CI (Linux) has none. The test project is in `E2E.slnx`, because it is plain .NET and builds anywhere.

To see the app and its tests in one solution, open [`samples/MobileApp.slnx`](../MobileApp.slnx) in Rider or Visual Studio. It holds the app, these tests, and the libraries the tests use. CI does not build it. The tests do not build the app: build and install it first, as above, or they run against an older build. The app targets iOS and Android, so a whole-solution build fails without the Android SDK, in the IDE and in `dotnet build samples/MobileApp.slnx`. Build the app on its own with the command above.

## Run the tests

Boot one simulator, then run the tests without `E2E_MOBILE_DEVICE`. The engine drives the booted device of the platform:

```bash
xcrun simctl boot "iPhone 17"
dotnet test samples/MobileApp.E2ETests
```

To pick a device yourself, set `E2E_MOBILE_DEVICE` to its UDID. List the UDIDs with `xcrun simctl list devices available`:

```bash
E2E_MOBILE_DEVICE=<UDID> dotnet test samples/MobileApp.E2ETests
```

A name such as `"iPhone 17"` works only when exactly one simulator has that name. Many machines have two simulators with the same name, one per runtime. If the variable is unset, no simulator is booted, and several are shut down, agent-device 0.21.35 selects the newest shut-down simulator for the platform (taken from its release notes; not run on a device). If the variable names a device that more than one simulator matches, the run fails with `AgentDeviceError` code `AMBIGUOUS_MATCH` and a message like `10 devices match this request equally; select one explicitly.` The details list the devices. Boot one simulator, or set `E2E_MOBILE_DEVICE` to a UDID from the list.

The first test installs the build, and every test launches the app fresh. When you are done, run `xcrun simctl shutdown all`. The agent-device daemon keeps running after the tests, in `~/.agent-device/e2e-dotnet`. Stop it with its `daemon.json` pid if you want it gone.

| Variable | Meaning |
| --- | --- |
| `E2E_MOBILE_PLATFORM` | `ios` or `android`. Unset: the `platform` of `targets[0]` in `e2e.config.json`. |
| `E2E_MOBILE_DEVICE` | Simulator or emulator UDID, or its name when only one device has that name. Unset: use the booted device. |
| `E2E_MOBILE_APP_PATH` | A build to install in place of `app.appPath` in `e2e.config.json`. |
| `E2E_MOBILE_PROVIDER` | `local` (default), `limrun`, or `devflow`. See [Run on Limrun](#run-on-limrun) and [Run through the DevFlow agent](#run-through-the-devflow-agent). |
| `LIMRUN_API_KEY` | The Limrun API key, for `E2E_MOBILE_PROVIDER=limrun`. |
| `E2E_DEVFLOW_TRACE` | `1` or `true` prints each call to the DevFlow agent (method, URL, status) to stderr. Run with `--logger "console;verbosity=detailed"` to see it. |
| `E2E_DEVFLOW_APP` | The app name the DevFlow broker lists, to pick among several agents. |
| `E2E_DEVFLOW_PORT` | The DevFlow agent's port, for `E2E_MOBILE_PROVIDER=devflow`. Unset: the broker is asked, then 9223. |

## Run on Limrun

The tests can run on a hosted [Limrun](https://limrun.com) iOS simulator instead of a local one. You need a Limrun API key and a build of the app for the simulator.

```bash
dotnet build samples/MobileApp -f net10.0-ios -r iossimulator-arm64 -p:ValidateXcodeVersion=false
export LIMRUN_API_KEY=...
E2E_MOBILE_PROVIDER=limrun dotnet test samples/MobileApp.E2ETests
```

A relative build path, the default or `E2E_MOBILE_APP_PATH`, is relative to the test project, on a local and on a hosted device. `dotnet test` reads a `.runsettings` file only with `--settings`, so pass it there if you use one.

`-p:ValidateXcodeVersion=false` is only needed when the installed Xcode is newer than the one the .NET iOS pack expects. `MobileAppTest` leases one Limrun device for the whole run, installs the build on it, and deletes it when the run ends. The device has a hard timeout of 15 minutes, so an interrupted run does not stay billed. `E2E_MOBILE_DEVICE` does not apply. On Android, set `E2E_MOBILE_PLATFORM=android` and `E2E_MOBILE_APP_PATH` to the `.apk`; the provider also needs `adb` from the Android SDK Platform-Tools on `PATH`. The Android path was not run. A run longer than the 15 minute timeout loses the device, so run a subset with `--filter` if the whole project is slow.

On a Limrun iOS device, keyboard actions such as `Locator.PressAsync("Enter")` fail with `UNSUPPORTED_CAPABILITY`. These tests tap the Greet button and do not press keys.

## Run through the DevFlow agent

The Debug build of the app registers the [MAUI DevFlow](https://github.com/dotnet/maui-labs/tree/main/src/DevFlow) agent (`builder.AddMauiDevFlowAgent()` in `MauiProgram.cs`). With `E2E_MOBILE_PROVIDER=devflow`, the tests drive the app through that agent instead of agent-device: no Node, and the MAUI visual tree with every `AutomationId`. The tests attach to a running app, so launch it first:

```bash
dotnet build samples/MobileApp -f net10.0-ios -r iossimulator-arm64 -t:Run
E2E_MOBILE_PROVIDER=devflow E2E_DEVFLOW_APP=MobileApp dotnet test samples/MobileApp.E2ETests
```

`MobileAppTest` finds the agent (through the DevFlow broker when one runs, `maui devflow list`, else on port 9223, or `E2E_DEVFLOW_PORT`), waits up to a minute for it, and holds its mutation lease for the run. Nothing is installed, and the app is not relaunched between tests, so each test starts from the state the last one left. The two device tests that send the app to the background or relaunch it are inconclusive in this mode. On Android, forward the agent's port with `adb forward tcp:<port> tcp:<port>` first. This mode has not been run against a device yet: it is the spike of the DevFlow integration.

## How the fixture works

`e2e.config.json` declares the app in `targets[0]` (platform, bundle id, and build path). The tests derive from `E2E.Mobile.NUnit.MobileAppTest`, which builds a `MobileEngine` from it, installs the build once per run, and launches the app fresh before each test. The settings that are not about the device, such as the agent, the model, and the cache, also come from the config.

On a hosted run, `MobileAppTest` leases the device once and every test shares it. The local run is unchanged: each test makes its own engine.

A device target has no URL, so a test does not call `App.OpenAsync()`. The base class launches the app with `Device.OpenAppAsync(AppId, new OpenAppOptions { Relaunch = true })`. A test reaches the `Device` fixture through the engine's active session.

The agent also gets `open_app`, `swipe`, and `alert` from `MobileTools.Create`, which the base class passes as `AgentTools`. A step that uses one of them is not replayed whole from the cache.

Agent steps take longer on a device than in a browser, so the base class raises `StepTimeout` to 90 seconds. The default of 30 seconds timed out a two-field step.
