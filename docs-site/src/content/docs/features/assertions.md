---
title: Soft assertions and evidence
description: Keep a failed check and continue, and save a redacted screenshot of the screen.
sidebar:
  label: "Soft assertions & evidence"
---

## Soft checks

- `Expect.Soft(locator)` and `Expect.Soft(screen)` record each failure and let the test go on. On NUnit the failures land on the test result, as inside `Assert.EnterMultipleScope`, and the test fails when it ends with every failure listed. On xUnit v3 they stay on the session, and the test fails when it is disposed, with every failure in one message.
- `Expect.Soft(Browser)` keeps the failures of `ToHaveURLAsync`, `ToHaveTitleAsync`, and `ToHaveClassAsync` the same way.
- `Expect.Poll(read)` retries a value. `ToBeAsync` and `ToSatisfyAsync(predicate)` work on both adapters. `E2E.NUnit` also adds `ToMatchAsync` with any NUnit constraint: `await Expect.Poll(() => ReadCountAsync()).ToMatchAsync(Is.GreaterThan(3));`. A read that throws, or a matcher that refuses the value's type, is one failing sample, not the end of the poll. A `Timeout` of `TimeSpan.Zero` sets no deadline: the poll runs until the cancellation token you pass ends.

## Evidence screenshots

`App.ScreenshotAsync(label)` saves a redacted screenshot under the attempt's `screenshots/` directory and returns the absolute path of the file it wrote (`<number>-<label>.png`); the report records the path relative to the artifact root. It masks secure fields only, after a `Secret` fill too, so a secret the app shows anywhere else is in the image. The screenshots a model sees (`Vision = On`, `Only`, the agent's own looks, `e2e mcp`, and `e2e explore` findings) work the same way: a generic secret filled into a plain input, or a value the app shows outside a secure field, reaches your model provider, so use test credentials. It fails with `UNSUPPORTED_CAPABILITY` only on an engine that keeps no screenshot artifact. The web, mobile, and Windows engines keep screenshot artifacts. A secret spelled into the label is redacted before it names the file.
