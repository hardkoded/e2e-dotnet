# MobileBenchmark tests

The tests that drive [`apps/MobileBenchmark`](../MobileBenchmark) with `MobileEngine`. They are the port of upstream's `apps/mobile-benchmark/tests`, laid out as upstream lays them out: each upstream test file is a directory, each `describe` a class, each `it` a test.

| Directory | Upstream | What it runs |
| --- | --- | --- |
| `Controls` | `controls.e2e.ts`, `describe('control inventory')` | The Fields and Gestures sections of the Control Inventory: `GetAttributeAsync`, long press, double tap, and `TapAsync(new TapOptions { Count = 3 })`. No model. |

All tests are in the `RealModel` and `Device` categories, so `TestCategory!=RealModel` skips them, and CI does too. They need a booted simulator or emulator and the app's build; see [`samples/MobileApp.E2ETests`](../../samples/MobileApp.E2ETests/README.md) for the prerequisites and `E2E_MOBILE_*` variables.

```bash
dotnet build apps/MobileBenchmark -f net11.0-ios -r iossimulator-arm64
dotnet test apps/MobileBenchmark.E2ETests
```

## Not ported yet

The app has the Fields (name field only) and Gestures sections of one scenario. The rest of upstream's mobile benchmark app and suites are tracked in the follow-up issue linked from the pull request.
