---
title: "PollOptions"
description: "How PollOptions) re-reads a value."
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


How `PollOptions)` re-reads a value.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Poll.cs#L10)

## Properties

<a id="E2E_PollOptions_Interval_"></a>
<a id="E2E_PollOptions_Interval"></a>

### Interval


```csharp
public TimeSpan? Interval { get; init; }
```


Pause between reads. The default is 100 milliseconds.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Poll.cs#L16)

<a id="E2E_PollOptions_Message_"></a>
<a id="E2E_PollOptions_Message"></a>

### Message


```csharp
public string? Message { get; init; }
```


Extra line in the timeout error.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Poll.cs#L19)

<a id="E2E_PollOptions_Timeout_"></a>
<a id="E2E_PollOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


Deadline for the matcher to hold. The default is 5 seconds.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Poll.cs#L13)
