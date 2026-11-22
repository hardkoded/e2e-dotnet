---
title: "PollOptions"
description: "How Poll<T> re-reads a value."
editUrl: false
prev: false
next: false
sidebar:
  label: "PollOptions"
  hidden: true
---

<a id="E2E_PollOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class PollOptions
```


How `Poll&lt;T&gt;` re-reads a value.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Poll.cs)

## Properties

<a id="E2E_PollOptions_Interval_"></a>
<a id="E2E_PollOptions_Interval"></a>

### Interval


```csharp
[Obsolete("Pass Intervals with one pause.")]
public TimeSpan? Interval { get; init; }
```


Pause between reads. Zero or a negative value is `INVALID_CONFIG`, and so is passing it with [Intervals](#E2E_PollOptions_Intervals).

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

:::caution[Obsolete]
This API is obsolete.
:::

<a id="E2E_PollOptions_Intervals_"></a>
<a id="E2E_PollOptions_Intervals"></a>

### Intervals


```csharp
public IReadOnlyList<TimeSpan>? Intervals { get; init; }
```


Pauses between reads. The default is 100, 250, 500 and 1000 milliseconds, and the last pause repeats. Each is a positive whole number of
milliseconds, and the list is not empty (`INVALID_CONFIG` otherwise).

**Value:** [IReadOnlyList&lt;TimeSpan&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_PollOptions_Message_"></a>
<a id="E2E_PollOptions_Message"></a>

### Message


```csharp
public string? Message { get; init; }
```


Extra line in the timeout error.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_PollOptions_Timeout_"></a>
<a id="E2E_PollOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


Deadline for the matcher to hold. Inside a test or a hosted session the default is the config's `assertionTimeout`, elsewhere 5 seconds,
and the deadline of the session or test the poll runs in caps it. Zero sets no deadline of its own: the poll runs until that deadline or the cancellation token it is
given ends, so it needs one that can end (`INVALID_CONFIG` otherwise). A negative value is `INVALID_CONFIG`.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)
