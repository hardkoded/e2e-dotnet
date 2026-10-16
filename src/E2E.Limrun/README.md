# E2E.Limrun

Limrun hosted iOS simulators and Android emulators for [`E2E.Mobile`](https://www.nuget.org/packages/E2E.Mobile), part of the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It ports `@e2e-dev/limrun`. Not an official TesterArmy product.

```
dotnet add package E2E.Limrun
```

`LimrunSimulators` leases one Limrun iOS simulator or Android emulator per worker, installs the app before the tests start, and deletes the device when the run ends.

```csharp
using E2E.Limrun;
using E2E.Mobile;

var options = new MobileOptions
{
    Platform = MobilePlatform.Ios,
    Provider = new LimrunSimulators(),
    // App = ...
};
```

Set `LIMRUN_API_KEY` in the environment that starts the tests, and `LIMRUN_BASE_URL` to reach another API. Android needs `adb` from the Android SDK Platform-Tools on `PATH`. `LimrunSimulatorsOptions` sets `Region`, `HardTimeout`, `Ios.Model`, `Android.Model`, and `Android.OsVersion`.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
