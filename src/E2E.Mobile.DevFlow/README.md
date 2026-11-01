# E2E.Mobile.DevFlow

.NET MAUI apps driven through the [MAUI DevFlow](https://github.com/dotnet/maui-labs/tree/main/src/DevFlow) agent, for [`E2E.Mobile`](https://www.nuget.org/packages/E2E.Mobile), part of the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It has no upstream counterpart. Not an official TesterArmy product.

```
dotnet add package E2E.Mobile.DevFlow
```

DevFlow is an HTTP agent that runs inside a MAUI app in Debug builds (`builder.AddMauiDevFlowAgent()` from the `Microsoft.Maui.DevFlow.Agent` package). This package holds an `IDeviceClient` over that agent and the provider, `DevFlowAgents`, that finds it. `MobileEngine` reads the app's visual tree from the agent instead of the OS accessibility tree, so every control reports its `AutomationId`, and no Node or agent-device daemon is needed.

```csharp
var engine = new MobileEngine(new MobileOptions
{
    Platform = MobilePlatform.Ios,
    Provider = new DevFlowAgents(),
    App = new MobileApp { BundleId = "com.example.app" },
});
```

The provider installs the target's build and launches the app on the booted simulator or emulator through `xcrun simctl` and `adb`, or attaches to an app you launched with `Launch = false`. This is a preview: deep links, alerts, permissions, orientation, and the home button on iOS are not supported yet, the agent exists only in Debug builds, and nothing has been run against a real device.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
