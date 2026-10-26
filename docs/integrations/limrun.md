---
title: Limrun
---

# Test mobile apps on Limrun

`E2E.Limrun` is a hosted device provider for the mobile engine. It ports upstream's `@e2e-dev/limrun` (`docs/integrations/limrun.mdx`). It creates a device for each worker, installs your app, and releases the device when the run ends. Use the standard `Screen` and `Expect` APIs to test your app's user flows.

## Install and authenticate

```bash
dotnet add package E2E.Mobile.NUnit      # or E2E.Mobile.XUnit.V3
dotnet add package E2E.Limrun
```

Set `LIMRUN_API_KEY` in the environment that starts the tests. In CI, store it as a secret. Use a key with permission to create and control instances. The provider reads the run's environment. It does not read the `lim login` session or `LIM_API_KEY`. `LIMRUN_BASE_URL` reaches another API.

The test runner needs the .NET SDK and Node 22.12 or newer. For Android, put `adb` from [Android SDK Platform-Tools](https://developer.android.com/tools/releases/platform-tools) on `PATH`. No local emulator is needed. The iOS runner needs no local Xcode or simulator. Build your app separately for an iOS simulator or as an Android APK.

## Configure a target

A test class that derives from `MobileAppTest` (see [Mobile](../get-started/mobile.md)) builds its `MobileOptions` in `CreateMobileOptions(IDeviceProvider? provider)`. Set `Provider` and point the app at your build:

```csharp
using E2E.Limrun;
using E2E.Mobile;

var options = new MobileOptions
{
    Platform = MobilePlatform.Ios,
    Provider = new LimrunSimulators(),
    App = new MobileApp
    {
        BundleId = "com.example.myapp",
        AppPath = "build/MyApp.app",
    },
};
```

For Android, use `MobilePlatform.Android`, the app's package name as `BundleId`, and an APK as `AppPath`. The provider installs the build before tests start. With `MobileAppTest`, set `E2E_MOBILE_PROVIDER=limrun` instead; see [Mobile](../get-started/mobile.md).

## Choose device options

`LimrunSimulatorsOptions` sets:

| Option | Default | Purpose |
| --- | --- | --- |
| `Region` | Limrun selects | Region preference, such as `eu` or `us-east1`. Capacity may cause overflow to another region. |
| `HardTimeout` | `1h` | Maximum device lifetime, including abandoned runs. Use a Go duration such as `30m` or `2h`. |
| `Ios.Model` | Limrun selects | `LimrunIosModel.Iphone` or `LimrunIosModel.Ipad`. |
| `Android.Model` | Limrun selects | `LimrunAndroidModel.Phone` or `LimrunAndroidModel.Tablet`. |
| `Android.OsVersion` | `15` | Android major version requested as a scheduling clue. |

Each worker gets an isolated agent-device daemon on the test runner and one Limrun instance. The daemon uses agent-device's Limrun driver for app control and recording. A device must become ready within five minutes. Installing the app is a separate setup step that this timeout does not cover. The provider cleans up after failed setup, and the engine releases all acquired devices when the run finishes or is interrupted. Choose a hard timeout longer than the run and short enough that an interrupted run does not stay billed.

Find a run's instances in Limrun by the `source=e2e` and `run=<run id>` labels. Recordings use the normal mobile artifact path.

## Check app compatibility

The Limrun iOS transport rejects keyboard actions such as pressing Enter with `UNSUPPORTED_CAPABILITY`. Tap the app's submit button when the flow provides one.

Test the locators and libraries your app uses on its target runtime. The [mobile app sample](../samples/mobile-app.md) demonstrates a native form flow on a Limrun device; it does not establish compatibility with every mobile framework or control.

## Differences from upstream

Upstream's page links to its `examples/with-limrun` app-flow example, a SwiftUI and a Compose app with five shared tests. The port does not have it; `samples/MobileApp` is its mobile example. See [COMPATIBILITY.md](../../COMPATIBILITY.md#mobileapptest-not-in-upstream).
