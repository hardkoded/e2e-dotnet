---
title: .NET MAUI
---

# Get started: .NET MAUI

A .NET MAUI app runs on iOS and Android, so `MobileEngine` drives it like any other mobile app. A Debug build can also be driven through the [MAUI DevFlow](https://github.com/dotnet/maui-labs/tree/main/src/DevFlow) agent, which needs no Node and sees the MAUI visual tree.

## 1. Give controls an `AutomationId`

`GetByTestId` finds a control by its `AutomationId`. Set one on every control a test touches. Use `SemanticProperties.Description` for the accessible name.

```xml
<Entry AutomationId="name" Placeholder="Ada Lovelace" SemanticProperties.Description="Name" />
<Button AutomationId="greet" Text="Greet" Clicked="OnGreetClicked" />
<Label AutomationId="greeting" />
```

## 2. Build the app

```bash
dotnet workload install maui
dotnet build MyApp -f net10.0-ios -r iossimulator-arm64      # iOS simulator
dotnet build MyApp -f net10.0-android -t:SignAndroidPackage  # Android
```

The iOS build writes `MyApp.app` under `bin/Debug/net10.0-ios/iossimulator-arm64/`. Build and install again after every app change: the tests do not build the app.

## 3. Create the test project

Keep it a plain `net10.0` project, so it builds on a machine without the MAUI workload.

```bash
dotnet new nunit -n MyApp.E2ETests
cd MyApp.E2ETests
dotnet add package E2E.Mobile.NUnit
```

```json
{
  "targets": [
    {
      "name": "myapp",
      "platform": "ios",
      "app": {
        "bundleId": "com.example.myapp",
        "appPath": "../MyApp/bin/Debug/net10.0-ios/iossimulator-arm64/MyApp.app"
      }
    }
  ],
  "agents": {
    "default": {
      "provider": "copilot",
      "model": "claude-sonnet-5.5",
      "context": "MyApp is a MAUI app with a greeting form."
    }
  },
  "cache": { "dir": ".e2e/cache" }
}
```

```csharp
using E2E.Mobile.NUnit;

public sealed class GreetingTests : MobileAppTest
{
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

## 4. Run

```bash
xcrun simctl boot "iPhone 17"
dotnet test --project .
```

For Android, boot an emulator and set `E2E_MOBILE_PLATFORM=android` and `E2E_MOBILE_APP_PATH` to the `.apk`.

## Drive the app through DevFlow (preview)

Add `Microsoft.Maui.DevFlow.Agent` to the app and register it in Debug builds:

```csharp
#if DEBUG
builder.AddMauiDevFlowAgent();
#endif
```

Launch the app, then run the tests against it:

```bash
dotnet build MyApp -f net10.0-ios -r iossimulator-arm64 -t:Run
E2E_MOBILE_PROVIDER=devflow E2E_DEVFLOW_APP=MyApp dotnet test --project .
```

The tests attach to the running app. Nothing is installed and the app is not relaunched between tests, so each test starts from the state the last one left. Tests that send the app to the background or relaunch it are inconclusive in this mode. Use `AttachesToRunningApp` in `Assume.That` to skip them. This mode is a preview and has not been run against a device yet.

## Next

- [Mobile app sample](../samples/mobile-app.md): this setup, complete.
- [Mobile](mobile.md): device controls, device choice, and hosted devices.
