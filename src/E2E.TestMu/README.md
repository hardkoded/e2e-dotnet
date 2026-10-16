# E2E.TestMu

[TestMu AI](https://www.testmuai.com) (formerly LambdaTest) hosted browsers for [`E2E`](https://www.nuget.org/packages/E2E), part of the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. TestMu has no upstream package; this is a .NET-only provider. Not an official TesterArmy product.

```
dotnet add package E2E.TestMu
```

`TestMuBrowsers` leases one browser on the TestMu web grid per worker slot. The engine attaches to it over CDP.

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

The TestMu dashboard shows these sessions without a Passed or Failed mark, because e2e passes no test result to a provider. It has not been checked against a real account: the TestMu docs connect to this URL with Playwright's own protocol, and this provider attaches over CDP.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for details.
