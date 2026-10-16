# E2E.TestMu.Mobile

TestMu AI real and virtual mobile devices for [`E2E.Mobile`](https://www.nuget.org/packages/E2E.Mobile), part of the community .NET port of the [e2e](https://github.com/tester-army/e2e) agentic testing framework. It has no upstream counterpart. Not an official TesterArmy product.

```
dotnet add package E2E.TestMu.Mobile
```

TestMu devices are driven through Appium, not agent-device. This package holds the Appium client for them (an `IDeviceClient`). `MobileEngine` uses it when its provider implements `IDeviceClientSource`.

This is a preview. The provider that leases a device and installs the app is not in the package yet, and the page source is not converted to a snapshot yet, so the engine cannot observe a screen.

See the [repository](https://github.com/hardkoded/e2e-dotnet) and its `COMPATIBILITY.md` for what differs from upstream.
