# E2E.Eas

EAS (Expo Application Services) hosted iOS simulators and Android emulators for [`E2E.Mobile`](https://www.nuget.org/packages/E2E.Mobile), part of the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It ports `@e2e-dev/eas`. Not an official TesterArmy product.

```
dotnet add package E2E.Eas
```

`EasSimulators` leases hosted simulators and emulators from Expo, one per worker, runs an EAS Build or an app archive on them, and stops the sessions when the run ends.

```csharp
using E2E.Eas;
using E2E.Mobile;

var options = new MobileOptions
{
    Platform = MobilePlatform.Ios,
    Provider = new EasSimulators(),
    // App = ...
};
```

Set `EXPO_TOKEN` in the environment that starts the tests, or log in with eas-cli. `EasSimulatorsOptions` sets the build, the app archive URL, the device, and the session limits. A dynamic app config (`app.config.ts`) needs Node on the path.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
