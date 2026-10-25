---
title: Mobile
---

# Get started: mobile (iOS and Android)

`MobileEngine` drives an iOS simulator or an Android emulator through [agent-device](https://github.com/callstack/agent-device). The tests use the same `Agent`, `Screen`, and `Expect` as web tests. For a .NET MAUI app, see [.NET MAUI](maui.md).

## Prerequisites

- macOS with Xcode and an iOS simulator runtime, or the Android SDK with an emulator.
- Node 22.12 or newer. `MobileEngine` starts agent-device through `npx`.
- Check the setup once: `npx --yes agent-device@0.21.29 doctor`.
- An app build: an `.app` bundle for the simulator, or an `.apk`.

## 1. Create the test project

```bash
dotnet new nunit -n MyApp.MobileTests
cd MyApp.MobileTests
dotnet add package E2E.Mobile.NUnit      # or E2E.Mobile.XUnit.V3
```

## 2. Point at your app

```json
{
  "targets": [
    {
      "name": "ios",
      "platform": "ios",
      "app": {
        "bundleId": "com.example.myapp",
        "appPath": "../MyApp/build/MyApp.app"
      }
    }
  ],
  "actionTimeout": 15000,
  "assertionTimeout": 10000,
  "agents": {
    "default": {
      "provider": "copilot",
      "model": "claude-sonnet-5.5",
      "context": "MyApp is a small demo app with a greeting form and a counter."
    }
  },
  "cache": { "dir": ".e2e/cache" }
}
```

`appPath` is relative to the folder of `e2e.config.json`. A device target has no URL. For Android, set `"platform": "android"` and `appPath` to the `.apk`, or run with `E2E_MOBILE_PLATFORM=android` and `E2E_MOBILE_APP_PATH`.

## 3. Write a test

Derive from `MobileAppTest`. It builds a `MobileEngine` from the config, installs the build once per run, launches the app fresh before each test, and gives the agent the `open_app`, `swipe`, and `alert` tools.

```csharp
using E2E.Mobile.NUnit;

public sealed class GreetingTests : MobileAppTest
{
    [Test]
    public async Task Greets_the_name_you_type()
    {
        await Screen.GetByTestId("name").FillAsync("Ada");
        await Screen.GetByTestId("greet").TapAsync();

        await Expect.That(Screen.GetByTestId("greeting")).ToHaveTextAsync("Hello, Ada!");
    }

    [Test]
    public async Task The_agent_gets_a_greeting()
    {
        await Agent.ActAsync("get the app to greet {name}", new ActOptions
        {
            Params = new Dictionary<string, object?> { ["name"] = "Grace" },
        });

        await Expect.That(Screen.GetByTestId("greeting")).ToHaveTextAsync("Hello, Grace!");
    }
}
```

`GetByTestId` finds a control by its accessibility identifier. `Device` is the device fixture: `HomeAsync`, `OpenAppAsync`, `ForegroundAppAsync`, and `SetAppearanceAsync(Appearance.Dark)`.

```csharp
await Device.HomeAsync();
await Device.OpenAppAsync(AppId, new OpenAppOptions { Relaunch = true });
```

## 4. Run

Boot exactly one simulator, then run:

```bash
xcrun simctl boot "iPhone 17"
dotnet test --project .
```

To pick a device, set `E2E_MOBILE_DEVICE` to a UDID (`xcrun simctl list devices available`). With two booted or matching devices the run fails with `AMBIGUOUS_MATCH`.

Tag device tests, for example `[assembly: Category("Device")]`, so a machine without a device can skip them.

## Hosted devices

Set `E2E_MOBILE_PROVIDER=limrun` with `LIMRUN_API_KEY` to run on a hosted Limrun simulator. `E2E.Eas` and `E2E.TestMu.Mobile` add other providers. See the [mobile sample README](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/MobileApp.E2ETests) for the full variable list.

## Next

- [Mobile app sample](../samples/mobile-app.md): a MAUI app and its tests, ready to run.
