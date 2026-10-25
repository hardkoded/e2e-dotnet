---
_layout: landing
title: Home
---

# E2E for .NET

Agentic end-to-end tests for .NET. Describe a goal in plain language, let an agent drive the app in Chromium, then check the result with locators. A verified agent step is recorded and replays on the next run with no model call.

```bash
dotnet add package E2E
dotnet add package E2E.NUnit   # or E2E.XUnit.V3 for xUnit v3
```

- [Guide](../README.md): install, write a test, config, model providers, and subscriptions.
- [API](api/index.md): every public type in `E2E`, `E2E.NUnit`, and `E2E.XUnit.V3`.
- [Rerun a test while you fix it](../README.md#rerun-a-test-while-you-fix-it): run one test with the replay cache on, so goals that passed replay without model calls.
- [Compatibility](../COMPATIBILITY.md): how this port differs from the TypeScript [e2e](https://github.com/tester-army/e2e).
- [Test filters](../COMPATIBILITY.md#test-filters): `ITestFilter`, `TestFilterContext`, and `TestFilterResult` leave out the tests a change cannot affect. The seam, the change detection (`INVALID_CHANGED_SINCE`), and the `run.changes` and `run.filters` report fields are ported; no `dotnet test` host calls a filter yet.

## Scrolling to a node

`Screen.ScrollUntilVisibleAsync(target, options)` scrolls the viewport until `target` resolves visibly. `Locator.ScrollUntilVisibleAsync(target, options)` swipes that node instead, so a feed or table that is its own scroll container pages without the pointer hovering it first.

```csharp
await Screen.ScrollUntilVisibleAsync(Screen.GetByRole("button", "Accept"));
await Screen.GetByTestId("feed").ScrollUntilVisibleAsync(row, new ScrollUntilVisibleOptions { Momentum = Momentum.Fast });
```

| Option | Type | Default |
| --- | --- | --- |
| `Direction` | `ScrollDirection` | `Down` |
| `Momentum` | `Momentum` (`None`, `Slow`, `Fast`) | `Slow` |
| `Timeout` | `TimeSpan` | 30 seconds |

`Timeout` is its own default, not `actionTimeout`, and bounds the whole scroll, each swipe included. `LOCATOR_NOT_FOUND` means the deadline expired first, a swipe it cut short included. `INVALID_LOCATOR` means `target` was made by another target's screen.

## Errors

A read or navigation that times out fails with `OPERATION_TIMEOUT`. It is not retried, even when the call log mentions a detached element or a navigation race. On a device, an input (a tap, key, swipe, or `Back`) that the device does not answer within `actionTimeout` fails with `ACTION_MAY_HAVE_COMMITTED` and is never repeated, because it may have landed; an input the timeout ran out before is not sent (`OPERATION_TIMEOUT`), and a long press that holds at least as long as the time left is refused (`NOT_ACTIONABLE`). See `EngineErrorCodes` in the [API](api/index.md).
