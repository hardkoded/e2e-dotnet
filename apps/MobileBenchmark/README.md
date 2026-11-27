# MobileBenchmark

The .NET MAUI counterpart of upstream's `apps/mobile-benchmark`, which is a React Native app. It holds one scenario so far, Control Inventory, and of that scenario only the Gestures section: a "Hold me" target, a "Double-tap me" target, `gesture-status`, and `tap-count`. Its tests are in [`MobileBenchmark.E2ETests`](../MobileBenchmark.E2ETests).

The bundle id and package are `dev.e2e.benchmark`, as upstream's. The home list shows a scenario as a row labeled `<name>. <description>`, and a test opens one by that label.

Build it as [`samples/MobileApp`](../../samples/MobileApp/README.md) is built:

```bash
dotnet build apps/MobileBenchmark -f net11.0-ios -r iossimulator-arm64
```
