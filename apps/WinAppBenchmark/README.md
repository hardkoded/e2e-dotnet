# WinAppBenchmark

A WinUI 3 desktop app of self-contained scenarios that are hard to automate, and the app the
`E2E.WinApp` engine is tested against. It is the Windows counterpart of upstream's
`apps/mobile-benchmark` (tester-army/e2e): the same scenarios, the same names, and the same
`AutomationId` where upstream has a `testID`. A test either handles the surface or fails for exactly
that reason.

The app is written with [Microsoft.UI.Reactor](https://github.com/microsoft/microsoft-ui-reactor)
(a declarative C# DSL over WinUI 3, no XAML) and runs unpackaged, so the engine can start it from its
executable.

## Build and run

Build for one platform; the project needs `Platform` set.

```bash
dotnet build apps/WinAppBenchmark -p:Platform=x64
apps/WinAppBenchmark/bin/x64/Debug/net10.0-windows10.0.22621.0/WinAppBenchmark.exe
```

Point a `windows` target at that executable to drive it with `e2e explore` or `e2e mcp`:

```json
{
  "targets": [
    { "platform": "windows", "app": { "appPath": "bin/x64/Debug/net10.0-windows10.0.22621.0/WinAppBenchmark.exe", "bundleId": "WinAppBenchmark" } }
  ]
}
```

## Layout

- `App/Examples.cs`: the catalog, upstream's `src/examples.ts`. One entry per scenario with its name,
  description, and component.
- `App/BenchmarkApp.cs`: the shell, upstream's `src/App.tsx`. A header with the route title
  (`screen-title`) and a Back button (`back`) stands where the mobile app has its navigation bar.
- `App/HomeList.cs`: two columns of compact rows. A row's `AutomationId` is the scenario name and its
  name is `"<name>. <description>"`.
- `Examples/<Scenario>.cs`: one component per scenario.

## Scenario contract

Every scenario ends in a deterministic success state. Where the UI Automation tree is intact, the
terminal element has `AutomationId` `success-message`. The adversarial scenarios (Broken
Accessibility, Vision Only, Flattened Registration Form, Flattened Login) put their controls in the
raw view and carry no ids, so success is only visible in pixels. The Login Form and Flattened Login
account is `tester@tester.army` / `benchmark123`.

## Differences from upstream

Not ported, because the surface exists on a phone only: Permission Prompt, Photo Picker, Stripe
PaymentSheet, Apple Pay, Apple Pay Billing Address, WebView Accessibility.

Changed to fit a desktop window:

| Scenario | Change |
| --- | --- |
| Modal Flow | The native alert is a `ContentDialog` (Confirm, Cancel) over an in-window modal card. |
| Bottom Tabs | A bottom tab row of buttons, with a Back button because a desktop has no swipe back. |
| Async States | A Refresh button (`refresh-button`) stands in for pull to refresh, which a mouse cannot do. |
| Choice Controls | No on-screen keyboard, so the `keyboard-warning` line and its gate are gone. |
| Sequential Onboarding | No keyboard covers Continue; each step still focuses its first field. |
| Infinite Scroll List, Huge Virtualized List | `ItemsRepeater` in a `ScrollView`: rows far from the viewport are not realized, so they are not in the tree. |
| Control Inventory | The Device tab keeps the window size, color scheme, and orientation lines; network, location, and biometrics are gone. The fields, toggles, and gestures tabs match upstream. |
| Vision Only, Broken Accessibility, Flattened forms | `AccessibilityView.Raw` hides the content from the UI Automation control view, where React Native uses `accessible` and `importantForAccessibility`. |
