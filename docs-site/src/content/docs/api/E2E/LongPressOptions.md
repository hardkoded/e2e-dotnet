---
title: "LongPressOptions"
description: "Options for CancellationToken)."
editUrl: false
prev: false
next: false
sidebar:
  label: "LongPressOptions"
  hidden: true
---

<a id="E2E_LongPressOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class LongPressOptions : ActionOptions
```


Options for `CancellationToken)`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ActionOptions](/api/E2E/ActionOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1232)

## Properties

<a id="E2E_LongPressOptions_Duration_"></a>
<a id="E2E_LongPressOptions_Duration"></a>

### Duration


```csharp
public TimeSpan? Duration { get; init; }
```


How long the press is held: whole milliseconds from 100 through 10000. Without one the engine holds its default.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1235)
