# E2E.Expo

Expo Simulators, hosted iOS simulators and Android emulators for [`E2E.Mobile`](https://www.nuget.org/packages/E2E.Mobile), part of the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It ports `@e2e-dev/eas`. Not an official TesterArmy product.

```
dotnet add package E2E.Expo
```

`ExpoSimulators` leases hosted simulators and emulators from Expo, one per worker, runs a build from Expo Build or an app archive on them, and stops the sessions when the run ends.

```csharp
using E2E.Expo;
using E2E.Mobile;

var options = new MobileOptions
{
    Platform = MobilePlatform.Ios,
    Provider = new ExpoSimulators(new ExpoSimulatorsOptions
    {
        BuildId = Environment.GetEnvironmentVariable("E2E_IOS_BUILD_ID"),
    }),
    // App = ...
};
```

Set `EXPO_TOKEN` in the environment that starts the tests, or log in with eas-cli. `ExpoSimulatorsOptions` sets the build, the app archive URL, the device, and the session limits. A dynamic app config (`app.config.ts`) needs Node on the path.

Set `E2E_IOS_BUILD_ID` to the ID of a simulator build, and read it into `BuildId` as above. Don't read `EAS_BUILD_ID`: in an Expo Workflows job, Expo sets it to the ID of the running job. In a workflow, set the variable in the test job's `env`, from the output of the build job it needs:

```yaml
jobs:
  e2e_ios:
    needs: [build_ios]
    env:
      E2E_IOS_BUILD_ID: ${{ needs.build_ios.outputs.build_id }}
```

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
