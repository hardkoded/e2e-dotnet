# E2E.TestMu

[TestMu AI](https://www.testmuai.com) (formerly LambdaTest) hosted browsers for [`E2E`](https://www.nuget.org/packages/E2E), part of the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. TestMu has no upstream package; this is a .NET-only provider. Not an official TesterArmy product.

```
dotnet add package E2E.TestMu
```

`TestMuBrowsers` leases one browser on the TestMu web grid per worker slot. The grid speaks Playwright's own protocol on its URL, so the engine connects with `Chromium.ConnectAsync` and opens its own browser context.

```csharp
using E2E.Engine;
using E2E.TestMu;

var options = new WebEngineOptions
{
    Browser = new TestMuBrowsers(new TestMuBrowsersOptions { Platform = "Windows 11" }),
    Viewport = null,
};
```

Set `LT_USERNAME` and `LT_ACCESS_KEY` (or `TESTMU_USERNAME` and `TESTMU_ACCESS_KEY`) in the environment that starts the tests. `TestMuBrowsersOptions` sets `BrowserName`, `BrowserVersion`, `Platform`, `Build`, `Video`, `Network`, `Console`, `TunnelName`, `GeoLocation`, and `Capabilities` for any other `LT:Options` entry.

The TestMu dashboard shows these sessions without a Passed or Failed mark, because e2e passes no test result to a provider. The grid ends a session when the connection closes.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for details.
