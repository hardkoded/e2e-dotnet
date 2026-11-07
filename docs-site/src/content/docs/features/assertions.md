---
title: Soft assertions and evidence
description: Keep a failed check and continue, and save a redacted screenshot of the screen.
sidebar:
  label: "Soft assertions & evidence"
---

## Soft checks

- `Expect.Soft(locator)` and `Expect.Soft(screen)` record each failure and let the test go on. On NUnit the failures land on the test result, as inside `Assert.EnterMultipleScope`, and the test fails when it ends with every failure listed. On xUnit v3 they stay on the session, and the test fails when it is disposed, with every failure in one message.
- `Expect.Soft(Browser)` keeps the failures of `ToHaveURLAsync`, `ToHaveTitleAsync`, and `ToHaveClassAsync` the same way.
- `Expect.Soft(screen, value, message)` keeps the failure of a value check the same way (see [Value checks](#value-checks)).
- `Expect.Poll(read)` retries a value. `ToBeAsync` and `ToSatisfyAsync(predicate)` work on both adapters. `E2E.NUnit` also adds `ToMatchAsync` with any NUnit constraint: `await Expect.Poll(() => ReadCountAsync()).ToMatchAsync(Is.GreaterThan(3));`. A read that throws, or a matcher that refuses the value's type, is one failing sample, not the end of the poll. A `Timeout` of `TimeSpan.Zero` sets no deadline: the poll runs until the cancellation token you pass ends.

## Value checks

`Expect.That(value, message)` checks a plain value at once, without retrying. For a value that may still change, use `Expect.Poll(read)`. The optional message opens every failure, and survives `Not`:

```csharp
var links = await Screen.GetByRole("link").CountAsync();
Expect.That(links, "the playground lists its pages").ToBeGreaterThanOrEqual(2);
Expect.That((double)links / links, "a ratio of a number to itself").ToBeCloseTo(1);
Expect.That(order).ToMatchObject(new { customer = new { Name = "Ada" }, Lines = new[] { new { Sku = "a" }, new { Sku = "b" } } });
Expect.That(payload).ToEqual(new { Id = Expect.StringMatching("^usr_"), CreatedAt = Expect.Any(typeof(DateTime)) });
```

| Matcher | Description |
| --- | --- |
| `ToBe` | Compares as JavaScript's `Object.is` does: `NaN` equals `NaN`, `0` is not `-0`, an object must be the same instance |
| `ToEqual` | Recursive structural equality, whatever the class: a record equals an object with its property names |
| `ToMatchObject` | Recursive subset match; an array matches element-wise with equal length |
| `ToBeTruthy`, `ToBeFalsy` | Truthy or falsy value |
| `ToBeNull`, `ToBeUndefined`, `ToBeDefined` | `null`, `null`, or not `null`: .NET has no `undefined` |
| `ToHaveLength` | `Length` or `Count` equals the expected number |
| `ToHaveProperty` | Property at a dotted path or a list of keys, optionally equal to a value |
| `ToContain` | String or collection containment |
| `ToMatch` | String substring or `Regex` match |
| `ToBeGreaterThan`, `ToBeGreaterThanOrEqual`, `ToBeLessThan`, `ToBeLessThanOrEqual` | Numeric bound holds |
| `ToBeCloseTo` | Within `10 ** -digits / 2` of the expected number; `digits` defaults to 2 |

A matcher that cannot evaluate its value fails as `ASSERTION_FAILED`, like a mismatch, as `ToContain` on a number does. `Not` inverts a matcher.

`Expect.Any(type)`, `Expect.Anything()`, `Expect.ObjectContaining(sample)`, `Expect.ArrayContaining(sample)`, `Expect.StringContaining(sample)`, and `Expect.StringMatching(sample)` stand in for a value inside `ToEqual`, `ToMatchObject`, `ToContain`, and `ToHaveProperty`. `Expect.Soft(screen, value, message)` keeps a failure for the end of the test instead of throwing it.

`ToMatchSchema` is not ported (there is no Standard Schema in .NET). `null` stands for both `null` and `undefined`.

## Evidence screenshots

`App.ScreenshotAsync(label)` saves a redacted screenshot under the attempt's `screenshots/` directory and returns the absolute path of the file it wrote (`<number>-<label>.png`); the report records the path relative to the artifact root. It masks secure fields only, after a `Secret` fill too, so a secret the app shows anywhere else is in the image. The screenshots a model sees (`Vision = On`, `Only`, the agent's own looks, `e2e mcp`, and `e2e explore` findings) work the same way: a generic secret filled into a plain input, or a value the app shows outside a secure field, reaches your model provider, so use test credentials. It fails with `UNSUPPORTED_CAPABILITY` only on an engine that keeps no screenshot artifact. The web, mobile, and Windows engines keep screenshot artifacts. A secret spelled into the label is redacted before it names the file.
