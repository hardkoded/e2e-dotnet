# Mobile app sample

A small .NET MAUI app ([`samples/MobileApp`](../MobileApp)) and the tests that drive it on a device with `MobileEngine`. It is the .NET counterpart of upstream's `examples/with-swiftui`: a name field with a Greet button, plus a counter.

The tests cover both ways to drive the app:

- `GreetingTests` finds controls by test id (the MAUI `AutomationId`) and taps and fills them. Three tests use no model. Two let the agent do the same steps, and check the result with a locator.
- `DeviceTests` uses the `Device` fixture: send the app home and bring it back, relaunch it, switch to dark mode, and read the foreground app.

All tests are in the `RealModel` and `Device` categories. They need a device, so `dotnet test E2E.slnx --filter "TestCategory!=RealModel"` skips them, and CI does too. To run the other real-model tests without these, use `--filter "TestCategory=RealModel&TestCategory!=Device"`.

## Prerequisites

- macOS with Xcode and an iOS simulator runtime, or an Android emulator (set `E2E_MOBILE_PLATFORM=android`).
- Node 22.12 or newer. `MobileEngine` starts [agent-device](https://github.com/callstack/agent-device) through `npx`, and agent-device drives the device. Check the setup once with `npx --yes agent-device@0.21.25 doctor`.
- The MAUI workload: `dotnet workload install maui`.
- A model login for the agent tests: `dotnet run --project src/E2E.Cli -- login github-copilot`, or an API key in `e2e.config.json`.

## Build the app

```bash
dotnet build samples/MobileApp -f net10.0-ios -r iossimulator-arm64
```

This writes `samples/MobileApp/bin/Debug/net10.0-ios/iossimulator-arm64/MobileApp.app`, the path the tests install by default. The Android path was not run. To try it, build `-f net10.0-android -t:SignAndroidPackage`, boot an emulator, and run the tests with `E2E_MOBILE_PLATFORM=android`. Set `E2E_MOBILE_APP_PATH` to the `.apk` if it is not at the default path.

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

A name such as `"iPhone 17"` works only when exactly one simulator has that name. Many machines have two simulators with the same name, one per runtime. If no device is booted and the variable is unset or ambiguous, the run fails with `AgentDeviceError` code `AMBIGUOUS_MATCH` and a message like `10 devices match this request equally; select one explicitly.` The details list the devices. Boot one simulator, or set `E2E_MOBILE_DEVICE` to a UDID from the list.

The first test installs the build, and every test launches the app fresh. When you are done, run `xcrun simctl shutdown all`. The agent-device daemon keeps running after the tests, in `~/.agent-device/e2e-dotnet`. Stop it with its `daemon.json` pid if you want it gone.

| Variable | Meaning |
| --- | --- |
| `E2E_MOBILE_PLATFORM` | `ios` (default) or `android`. |
| `E2E_MOBILE_DEVICE` | Simulator or emulator UDID, or its name when only one device has that name. Unset: use the booted device. |
| `E2E_MOBILE_APP_PATH` | A build to install in place of the default. |
| `E2E_MOBILE_PROVIDER` | `local` (default) or `limrun`. See [Run on Limrun](#run-on-limrun). |
| `LIMRUN_API_KEY` | The Limrun API key, for `E2E_MOBILE_PROVIDER=limrun`. |

## Run on Limrun

The tests can run on a hosted [Limrun](https://limrun.com) iOS simulator instead of a local one. You need a Limrun API key and a build of the app for the simulator.

```bash
dotnet build samples/MobileApp -f net10.0-ios -r iossimulator-arm64 -p:ValidateXcodeVersion=false
export LIMRUN_API_KEY=...
E2E_MOBILE_PROVIDER=limrun dotnet test samples/MobileApp.E2ETests
```

A relative build path, the default or `E2E_MOBILE_APP_PATH`, is relative to the test project, on a local and on a hosted device. `dotnet test` reads a `.runsettings` file only with `--settings`, so pass it there if you use one.

`-p:ValidateXcodeVersion=false` is only needed when the installed Xcode is newer than the one the .NET iOS pack expects. `HostedDeviceSetup` leases one Limrun device for the whole run, installs the build on it, and deletes it when the run ends. The device has a hard timeout of 15 minutes, so an interrupted run does not stay billed. `E2E_MOBILE_DEVICE` does not apply. On Android, set `E2E_MOBILE_PLATFORM=android` and `E2E_MOBILE_APP_PATH` to the `.apk`; the provider also needs `adb` from the Android SDK Platform-Tools on `PATH`. The Android path was not run. A run longer than the 15 minute timeout loses the device, so run a subset with `--filter` if the whole project is slow.

On a Limrun iOS device, keyboard actions such as `Locator.PressAsync("Enter")` fail with `UNSUPPORTED_CAPABILITY`. These tests tap the Greet button and do not press keys.

## How the fixture works

`e2e.config.json` has no `targets`: the config only supports the web platform. `MobileAppTest` overrides `CreateEngine()` to return a `MobileEngine` with `MobileOptions` (platform, device, and the app's bundle id and build path). The settings that are not about the device, such as the agent, the model, and the cache, still come from the config.

`HostedDeviceSetup` runs the engine's prepare and finish hooks, which the NUnit adapter does not, so that a hosted run leases the device once and every test shares it. The local run is unchanged: each test makes its own engine.

A device target has no URL, so a test does not call `App.OpenAsync()`. The base class launches the app with `Device.OpenAppAsync(AppId, new OpenAppOptions { Relaunch = true })`. A test reaches the `Device` fixture through the engine's active session.

The agent also gets `open_app`, `swipe`, and `alert` from `MobileTools.Create`, which the base class passes as `AgentTools`. A step that uses one of them is not replayed whole from the cache.

Agent steps take longer on a device than in a browser, so the base class raises `StepTimeout` to 90 seconds. The default of 30 seconds timed out a two-field step.
