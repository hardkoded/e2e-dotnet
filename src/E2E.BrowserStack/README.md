# E2E.BrowserStack

BrowserStack App Automate real iOS and Android devices for [`E2E.Mobile`](https://www.nuget.org/packages/E2E.Mobile), part of the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It ports `@e2e-dev/browserstack`. Not an official TesterArmy product.

```
dotnet add package E2E.BrowserStack
```

`BrowserStackDevices` leases one BrowserStack real device per worker, installs the app when the session starts, and ends the session when the run ends. Each device runs through its own agent-device daemon, which uses agent-device's built-in `browserstack` provider.

```csharp
using E2E.BrowserStack;
using E2E.Mobile;

var options = new MobileOptions
{
    Platform = MobilePlatform.Android,
    Provider = new BrowserStackDevices(new BrowserStackDevicesOptions
    {
        Device = "Google Pixel 9",
        OsVersion = "16.0",
        App = "bs://<app id>", // or an https URL, or a local path
    }),
};
```

Set `BROWSERSTACK_USERNAME` and `BROWSERSTACK_ACCESS_KEY` in the environment that starts the tests. The key reaches the daemon alone and is never logged. `BROWSERSTACK_API_ENDPOINT` reaches another App Automate API. The provider records BrowserStack's own video of the session and links it in the report.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
