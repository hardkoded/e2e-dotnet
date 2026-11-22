---
title: Soft assertions and evidence
description: Keep a failed check and continue, and save a redacted screenshot of the screen.
sidebar:
  label: "Soft assertions & evidence"
---

## Soft checks

- `Expect.That(locator, message)`, `Expect.That(screen, message)`, and their `Expect.Soft` forms take an optional message. It opens each assertion failure of the matchers, `Not` included, before the matcher and the locator, and the `LOCATOR_AMBIGUOUS` error of a locator that matches more than one node (also from `ToHaveScreenshotAsync`). An invalid option or a denied read fails without it. `Expect.That(Browser)` takes none; its failure names the matcher.

  ```csharp
  await Expect.That(Screen.GetByTestId("cart-empty"), "the cart is empty").ToBeVisibleAsync();
  // ASSERTION_FAILED: the cart is empty: expect.toBeVisible failed
  // locator: getByTestId("cart-empty")
  ```

- `Expect.Soft(locator)` and `Expect.Soft(screen)` record each failure and let the test go on. On NUnit the failures land on the test result, as inside `Assert.EnterMultipleScope`, and the test fails when it ends with every failure listed. On xUnit v3 they stay on the session, and the test fails when it is disposed, with every failure in one message.
- `Expect.Soft(Browser)` keeps the failures of `ToHaveURLAsync`, `ToHaveTitleAsync`, and `ToHaveClassAsync` the same way.
- `Expect.Soft(screen, value, message)` keeps the failure of a value check the same way (see [Value checks](#value-checks)).
- `Expect.Poll(read)` retries a value. `ToBeAsync` and `ToSatisfyAsync(predicate)` work on both adapters. `E2E.NUnit` also adds `ToMatchAsync` with any NUnit constraint: `await Expect.Poll(() => ReadCountAsync()).ToMatchAsync(Is.GreaterThan(3));`. A read that throws, or a matcher that refuses the value's type, is one failing sample, not the end of the poll. Without a `Timeout`, a poll inside a test or a hosted session waits for the config's `assertionTimeout`, and `toPass` takes the config's `toPass` defaults the same way. A poll ends with the deadline of the session or test it runs in, however long its own `Timeout`: it fails with the session's timeout, and the poll's last failed assertion (`stopped at the attempt deadline after N ms`) is reported beside it as a secondary error. It also stops when the session closes or the token the session or host was opened with is cancelled. A `Timeout` of `TimeSpan.Zero` sets no deadline of its own: the poll runs until the session's deadline or the cancellation token you pass ends. `PollOptions.Intervals` lists the pauses between reads, 100, 250, 500, then 1000 ms by default, and the last pause repeats. `PollOptions.Interval` is deprecated: pass `Intervals` with one pause. It warns once on stderr, and passing both is `INVALID_CONFIG`.


## Retrying a block

`Expect.That(block).ToPassAsync(options)` retries a block until it stops throwing. Use it when several assertions must pass in the same try. For a single changing value, use `Expect.Poll(read)`. The block is an `Action`, a `Func<Task>`, or a `Func<CancellationToken, Task>`. The optional message opens the timeout error.

```csharp
await Expect
    .That(
        async () =>
        {
            var response = await http.GetAsync("/api/orders/42");
            Expect.That((int)response.StatusCode).ToBe(200);
            await Expect.That(Screen.GetByRole("status")).ToHaveTextAsync("Ready", timeout: TimeSpan.FromMilliseconds(200));
        },
        "the order is ready"
    )
    .ToPassAsync(new PassOptions { Timeout = TimeSpan.FromSeconds(5) });
```

| Option | Type | Default |
| --- | --- | --- |
| `Timeout` | `TimeSpan?` | `toPass.timeout` of the config, else `TimeSpan.Zero` |
| `Intervals` | `IReadOnlyList<TimeSpan>?` | `toPass.intervals` of the config, else 100, 250, 500, then 1000 ms |

- `Timeout` is the deadline, from zero through `2147483647` ms. At zero there is no timeout of its own: the block is retried until the cancellation token you pass ends, so outside a test a positive `Timeout` (or a token that can end) is required, or the call fails with `INVALID_CONFIG`.
- `Intervals` are the pauses between tries, each a positive whole number of milliseconds. The last pause repeats.
- A try fails when the block throws. The next try comes after the next pause. A `SkipException` ends the retry and propagates.
- `Not.ToPassAsync()` passes on the first try that throws, and fails at the deadline when every try passed (`last: the block passed`).
- `Expect.Soft(screen, block).ToPassAsync()` keeps a timeout as a soft failure, and the test goes on.
- Inside the block, give a locator matcher a short `timeout`, so more tries fit before the deadline. Use `Expect`, not `Expect.Soft`, since a soft failure never fails a try.
- Each try gets a token that ends when the try fails or is cut at the deadline, so pass it on to steps and nested polls.
- Await every `ToPassAsync` call.

At the deadline it throws `ASSERTION_FAILED` that names the call, then the message when given, then the last error, which is also the inner exception:

```text
expect(...).toPass() timed out after 5000 ms
the order is ready
last: expected 202 to be 200
```

The last line is `last: no try completed` when no try finished. A value that is not a block is `INVALID_ARGUMENT`. A negative or too long `Timeout`, an empty or invalid `Intervals`, or no positive timeout outside a test is `INVALID_CONFIG`, before the first try.

Set defaults for every call with the `toPass` config key: `"toPass": { "timeout": 10000, "intervals": [100, 250, 500] }`. A call's own options win. Not ported: the phase deadline of a test or hook, the last error recorded under `secondaryErrors`, and the hook-deadline defaults in `beforeAll` and `afterAll`. The port has no runner phases, so the caller's cancellation token bounds a block with no timeout.

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
| `ToBeNull`, `ToBeUndefined`, `ToBeDefined` | `null`, `Expect.Undefined`, or anything but `Expect.Undefined` (`null` is defined) |
| `ToHaveLength` | `Length` or `Count` equals the expected number |
| `ToHaveProperty` | Property at a dotted path or a list of keys, optionally equal to a value |
| `ToContain` | String or collection containment |
| `ToMatch` | String substring or `Regex` match |
| `ToBeGreaterThan`, `ToBeGreaterThanOrEqual`, `ToBeLessThan`, `ToBeLessThanOrEqual` | Numeric bound holds |
| `ToBeCloseTo` | Within `10 ** -digits / 2` of the expected number; `digits` defaults to 2 |

A matcher that cannot evaluate its value fails as `ASSERTION_FAILED`, like a mismatch, as `ToContain` on a number does. `Not` inverts a matcher.

`Expect.Any(type)`, `Expect.Anything()`, `Expect.ObjectContaining(sample)`, `Expect.ArrayContaining(sample)`, `Expect.StringContaining(sample)`, and `Expect.StringMatching(sample)` stand in for a value inside `ToEqual`, `ToMatchObject`, `ToContain`, and `ToHaveProperty`. `Expect.Soft(screen, value, message)` keeps a failure for the end of the test instead of throwing it.

`ToMatchSchema` takes a `StandardSchemaV1<TOutput>` delegate, since there is no Standard Schema in .NET: it returns the schema's output (defaults applied) when the value matches, lists every issue by its path when it does not, and negated returns `null`. A schema that validates asynchronously cannot be passed. `Expect.Soft(screen, value).ToMatchSchema(schema)` keeps a failure for the end of the test and returns `default` (`null` for a reference output, `0` for an int) instead of `undefined`. `Expect.Undefined` is `undefined`, which C# has no value for: `null` is only `null`. `ToEqual` ignores a member that holds `Expect.Undefined`, and `Expect.Anything()` matches neither `null` nor `Expect.Undefined`.

## Evidence screenshots

`App.ScreenshotAsync(label)` saves a redacted screenshot under the attempt's `screenshots/` directory and returns the absolute path of the file it wrote (`<number>-<label>.png`); the report records the path relative to the artifact root. It masks secure fields only, after a `Secret` fill too, so a secret the app shows anywhere else is in the image. The screenshots a model sees (`Vision = On`, `Only`, the agent's own looks, `e2e mcp`, and `e2e explore` findings) work the same way: a generic secret filled into a plain input, or a value the app shows outside a secure field, reaches your model provider, so use test credentials. It fails with `UNSUPPORTED_CAPABILITY` only on an engine that keeps no screenshot artifact, and with `POLICY_DENIED` on iOS and Android when a secure field on screen has no bounds to mask. The web, mobile, and Windows engines keep screenshot artifacts. A secret spelled into the label is redacted before it names the file.
