---
title: "PassOptions"
description: "How ToPassAsync retries a block. Upstream's PassOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "PassOptions"
  hidden: true
---

<a id="E2E_PassOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class PassOptions
```


How `ToPassAsync` retries a block. Upstream's `PassOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/PassOptions.cs)

## Properties

<a id="E2E_PassOptions_Intervals_"></a>
<a id="E2E_PassOptions_Intervals"></a>

### Intervals


```csharp
public IReadOnlyList<TimeSpan>? Intervals { get; init; }
```


Pauses between tries. The default is 100, 250, 500 and 1000 milliseconds, and the last pause repeats. Each is a positive whole number of
milliseconds, and the list is not empty (`INVALID_CONFIG` otherwise).

**Value:** [IReadOnlyList&lt;TimeSpan&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_PassOptions_Timeout_"></a>
<a id="E2E_PassOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


Deadline for the block to pass. By default only the test's deadline applies, so it needs a cancellation token that can end (`INVALID_CONFIG`
otherwise). The `toPass` config key sets another default. A negative value is `INVALID_CONFIG`.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)
