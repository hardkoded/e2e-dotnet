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

## How to use it

1. **Add the agent to the app.** Reference `Microsoft.Maui.DevFlow.Agent` in Debug builds only, and register it:

   ```xml
   <PackageReference Include="Microsoft.Maui.DevFlow.Agent" Version="0.1.0-preview.13.26512.5" Condition="'$(Configuration)' == 'Debug'" />
   ```

   ```csharp
   #if DEBUG
   builder.AddMauiDevFlowAgent();
   #endif
   ```

2. **Give the controls an `AutomationId`.** The tests find a control by it (`Screen.GetByTestId("counter")`).
3. **Add this package to the test project** and pass the provider in `MobileOptions.Provider`, as in the snippet above. `DevFlowAgentsOptions` sets the `Port`, the `AppName` that picks one agent when several run, and the `ConnectTimeout` (60 seconds).
4. **Launch the Debug build yourself.** On an iOS simulator:

   ```bash
   dotnet build -f net10.0-ios -r iossimulator-arm64 -t:Run
   ```

   Add `-p:ValidateXcodeVersion=false` when the installed Xcode is newer than the one the .NET iOS pack expects.
5. **Run the tests.** The provider waits for the agent, checks that it runs the app the target names, and holds the agent's mutation lease for the run, so the DevFlow Inspector shows who drives the app. It gives the lease back when the run ends and leaves the app running.

The provider finds the agent in this order: `DevFlowAgentsOptions.Port`, the one agent of the platform and `AppName` that a running DevFlow broker lists (port 19223, started by the `maui` CLI), then port 9223. To check it by hand, run `curl http://localhost:9223/api/v1/agent/status`.

The app is not relaunched between tests, so a test starts from the state the last one left. Write tests that do not depend on a fresh launch, or relaunch the app yourself before the run.

The [sample](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/MobileApp.E2ETests) runs this way with `E2E_MOBILE_PROVIDER=devflow`.

This is a preview. It attaches to an app that is already running: start the app yourself, then run the tests. `device.installApp()`, a relaunch, deep links, the home screen, alerts, permissions, and orientation are not supported yet, and the agent exists only in Debug builds.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
