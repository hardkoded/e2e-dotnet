# WinAppBenchmark tests

The tests that drive [`apps/WinAppBenchmark`](../WinAppBenchmark) with `WinAppEngine`. They are the port of upstream's `apps/mobile-benchmark` suites, laid out as upstream lays them out: each upstream test file is a directory, each `describe` a class, each `it` a test.

| Directory | Upstream | What it runs |
| --- | --- | --- |
| `Smoke`, `LoginForm`, `Flows`, `FlowsLists` | `smoke`, `login-form`, `flows`, `flows-lists` | Locator-only runs through the scenarios. No model. |
| `Controls` | `controls` | Every locator verb, read, and matcher on the Control Inventory. No model. |
| `Device`, `DeviceFixture` | `device`, `device-fixture` | The `Window` fixture and the onboarding wizard. No model. |
| `Scenarios` | `tests-agent/scenarios` | One `Agent.ActAsync` per scenario, checked with a locator or, for the hidden-tree scenarios, with a vision assert. |

All tests are in the `RealModel` and `Windows` categories. They need a Windows desktop that is unlocked and not in use: keys, clicks, and the wheel go to the window in the foreground. The agent tests also need a model login. `dotnet test E2E.slnx --filter "TestCategory!=RealModel"` skips them, and CI does too.

## Run

```bash
dotnet build apps/WinAppBenchmark -p:Platform=x64
dotnet run --project src/E2E.Cli -- login github-copilot        # once, for the agent tests
dotnet test apps/WinAppBenchmark.E2ETests                       # everything
dotnet test apps/WinAppBenchmark.E2ETests --filter "FullyQualifiedName!~Scenarios"   # no model
```

`e2e.config.json` names the executable the build above writes, relative to its folder. `E2E_WINAPP_APP_PATH` names another. The account of the Login Form scenario is `tester@tester.army` / `benchmark123`; `E2E_USER_BENCHMARK_USERNAME` and `E2E_USER_BENCHMARK_PASSWORD` override it.

## Not ported

Each is named with its reason in the test file it would live in.

- Tests of scenarios the app does not have: Permission Prompt, Photo Picker, Stripe PaymentSheet, Apple Pay, Apple Pay Billing Address, WebView Accessibility.
- Tests of a fixture a desktop window lacks: appearance, clipboard, home, network, airplane mode, location, biometrics, `dismissKeyboard`.
- Tests of locator members the port does not have: `tapAt`, a tap position, `screen.swipe`, `screen.scrollUntilVisible`, `app.screenshot`. Where a flow needs one, the test uses the window session (`Session.SwipeAsync`, `Session.LongPressAsync`, `Session.DragToAsync`) or a helper in `Fixtures/BenchmarkTest.cs`.
- `describe('refusals')`: it checks verbs a mobile device never declares, and the Windows engine declares them.
- The placeholder query of the Control Inventory: a UI Automation edit exposes no placeholder.

Tests that differ from their upstream `it`:

- `Home_lists_the_scenarios_and_opens_one` goes back with the app's Back button, since `App.BackAsync` is `UNSUPPORTED_CAPABILITY` on a desktop app.
- `Async_states_load_refresh_claim_through_the_toast` presses the Refresh button, since a mouse cannot pull to refresh.
- `Gestures_long_press_then_swipe_left` swipes by dragging the card to the Back button on its left.
- `SetOrientation_rotates_the_window_the_app_measures` resizes the window.
- `An_ambiguous_locator_fails_at_once` expects `LOCATOR_AMBIGUOUS`, upstream's code for an ambiguous locator.
