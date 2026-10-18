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

You need a .NET MAUI app, the .NET SDK, and for iOS a Mac with Xcode and a simulator. The steps below were run on an iOS simulator. Android has the same agent, but it was not run.

### 1. Add the agent to the app

The agent ships as the `Microsoft.Maui.DevFlow.Agent` package. Reference it in Debug builds only, and register it:

```xml
<PackageReference Include="Microsoft.Maui.DevFlow.Agent" Version="0.1.0-preview.13.26512.5" Condition="'$(Configuration)' == 'Debug'" />
```

```csharp
#if DEBUG
builder.AddMauiDevFlowAgent();
#endif
```

Give each control a test calls an `AutomationId`. The tests find a control by it:

```xml
<Label x:Name="CounterLabel" AutomationId="counter" Text="Count: 0" />
<Button AutomationId="increment" Text="Add one" Clicked="OnIncrement" />
```

### 2. Set up the test project

Add `E2E`, `E2E.NUnit`, `E2E.Mobile`, and this package (`dotnet add package E2E.Mobile.DevFlow`). Put an `e2e.config.json` next to the tests. It holds the agent's model and a short description of the app, as in the [sample](https://github.com/hardkoded/e2e-dotnet/tree/main/samples/MobileApp.E2ETests/e2e.config.json). Steps that use the agent (`Agent.ActAsync`) need a model login. Steps that only use `Screen` and `Expect` do not.

The NUnit adapter starts one engine per test and does not run the engine's prepare and finish hooks. So one `[SetUpFixture]` runs them once for the whole run. Every test then shares one engine, and so one lease on the agent:

```csharp
using E2E;
using E2E.Mobile;
using E2E.Mobile.DevFlow;

[SetUpFixture]
public sealed class DevFlowSetup
{
    private static readonly MobileOptions Options = new()
    {
        Platform = MobilePlatform.Ios,
        TargetName = "myapp",
        App = new E2E.Mobile.MobileApp { BundleId = "com.example.myapp" },
        Provider = new DevFlowAgents(new DevFlowAgentsOptions { AppName = "MyApp" }),
    };

    private static readonly string RunId = Guid.NewGuid().ToString("N")[..12];
    private static readonly Dictionary<string, string?> Env = Environment.GetEnvironmentVariables()
        .Cast<System.Collections.DictionaryEntry>()
        .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value, StringComparer.Ordinal);

    internal static MobileEngine Engine { get; } = new(Options);

    [OneTimeSetUp]
    public Task AttachAsync() => Engine.PrepareAsync(
        new MobilePrepareInfo { RunId = RunId, TargetName = "myapp", ProjectRoot = E2EConfig.Discover().ProjectRoot, Slots = 1, Env = Env, App = Options.App, Log = NUnit.Framework.TestContext.Progress.WriteLine },
        NUnit.Framework.TestContext.CurrentContext.CancellationToken);

    [OneTimeTearDown]
    public Task ReleaseAsync() => Engine.FinishAsync(
        new MobileFinishInfo { RunId = RunId, TargetName = "myapp", Env = Env, Log = NUnit.Framework.TestContext.Progress.WriteLine },
        CancellationToken.None);
}
```

Then a base class that hands the shared engine to each test, and a test:

```csharp
using E2E.Engine;
using E2E.NUnit;

public abstract class MyAppTest : E2ETest
{
    protected override IEngine CreateEngine() => DevFlowSetup.Engine;

    // Checks that the agent runs the app the target names. It does not launch or relaunch it.
    [SetUp]
    public Task OpenAppAsync() =>
        DevFlowSetup.Engine.ActiveSessions.Single().Device.OpenAppAsync("com.example.myapp");
}

public sealed class CounterTests : MyAppTest
{
    [Test]
    public async Task The_button_adds_one()
    {
        await Screen.GetByTestId("increment").TapAsync();

        await Expect.That(Screen.GetByTestId("counter")).ToHaveTextAsync("Count: 1");
    }
}
```

The sample's [`HostedDeviceSetup`](https://github.com/hardkoded/e2e-dotnet/blob/main/samples/MobileApp.E2ETests/HostedDeviceSetup.cs) and [`MobileAppTest`](https://github.com/hardkoded/e2e-dotnet/blob/main/samples/MobileApp.E2ETests/MobileAppTest.cs) are the full versions. They also run the same tests on a local device or a hosted one.

### 3. Launch the app

The agent lives inside the app, so the app must run before the tests start. On an iOS simulator:

```bash
xcrun simctl boot "iPhone 17"          # skip if a simulator is already booted
dotnet build -f net10.0-ios -r iossimulator-arm64 -p:ValidateXcodeVersion=false
xcrun simctl install booted bin/Debug/net10.0-ios/iossimulator-arm64/MyApp.app
xcrun simctl launch booted com.example.myapp
```

Run the build in the app's folder. `-p:ValidateXcodeVersion=false` is only needed when the installed Xcode is newer than the one the .NET iOS pack expects ("requires Xcode 27.0. The current version of Xcode is 27.1"). To check that the agent answers, run `curl http://localhost:9223/api/v1/agent/status`. It returns the app's name and package id.

### 4. Run the tests

```bash
dotnet test
```

The provider waits up to a minute for the agent. It checks that the agent runs the app and platform the target names. Then it claims the agent's mutation lease as holder `e2e`, so the DevFlow Inspector shows who drives the app. When the run ends, it gives the lease back and leaves the app running.

### Finding the agent

The provider looks in this order:

1. `DevFlowAgentsOptions.Port`.
2. The one agent of the platform and `AppName` that a running DevFlow broker lists. The broker listens on port 19223. The `maui` CLI starts it, never the app or this package. Set `UseBroker = false` to skip it.
3. Port 9223, the agent's default.

If several agents match at step 2, the run fails and names them. Set `AppName` or `Port` to pick one.

| Option | Default | Meaning |
|---|---|---|
| `Host` | `localhost` | Where the agent listens. |
| `Port` | none | The agent's port. Skips the broker. |
| `UseBroker` | `true` | Ask a running broker for the agent's port. |
| `BrokerPort` | `19223` | The broker's port. |
| `AppName` | none | The app name the broker lists, to pick one agent. |
| `ConnectTimeout` | 60 seconds | How long to wait for the agent to answer. |

### Things to know

- **The app is not relaunched between tests.** A test starts from the state the last one left, and the run starts from the state the app is in when you launch the tests. Write tests that do not depend on a fresh launch (for example, read the counter first and assert the change), or relaunch the app yourself before the run.
- **One agent is one app.** A target with more than one worker slot fails with `INVALID_CONFIG`.
- **Only one host holds the lease.** If the DevFlow Inspector or another test run holds it, the run fails and names the holder. Close that host, or wait ten seconds after it stops.
- **A tree read that fails with 409 is tried again.** The agent answers 409 when the screen moves during a read, as it does during an animation. The read is tried up to four times.
- **Set `E2E_DEVFLOW_TRACE=1` to see the calls.** Each call to the agent prints its method, URL, and status to stderr. Calls go to localhost, so a system proxy such as Proxyman does not show them. Bodies are not printed: they hold typed text.

### When it fails

| What you see | What it means |
|---|---|
| `the DevFlow agent at http://localhost:9223/ could not be reached` after 60 seconds | The app is not running, it is a Release build, or it did not register the agent. Run the `curl` check above. If the agent uses another port, set `Port`. |
| `runs com.example.other, not com.example.myapp` | The agent belongs to another app. Launch the right one, or fix the target's `BundleId`. |
| `runs a ... app, but target ... is ios` | The agent belongs to an app on another platform. Set `Platform` or `AppName`. |
| `N DevFlow agents match` | More than one app runs the agent. Set `AppName` or `Port`. |
| `another DevFlow host holds the app's mutation lease` | The Inspector or another run drives the app. Close it, or wait for its lease to lapse. |
| `UNSUPPORTED_CAPABILITY` | The step needs something the agent cannot do yet: a relaunch, launch arguments, a deep link, install, the home screen, an alert, permissions, or orientation. |

This is a preview. It attaches to an app that is already running: start the app yourself, then run the tests. `device.installApp()`, a relaunch, deep links, the home screen, alerts, permissions, and orientation are not supported yet, and the agent exists only in Debug builds.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
