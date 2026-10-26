---
title: "GotoOptions"
description: "Options of CancellationToken)."
editUrl: false
prev: false
next: false
sidebar:
  label: "GotoOptions"
  hidden: true
---

<a id="E2E_GotoOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record GotoOptions : IEquatable<GotoOptions>
```


Options of `CancellationToken)`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;GotoOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/GotoOptions.cs#L21)

## Properties

<a id="E2E_GotoOptions_Timeout_"></a>
<a id="E2E_GotoOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


The navigation budget. Defaults to the action timeout.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/GotoOptions.cs#L27)

<a id="E2E_GotoOptions_WaitUntil_"></a>
<a id="E2E_GotoOptions_WaitUntil"></a>

### WaitUntil


```csharp
public GotoWaitUntil WaitUntil { get; init; }
```


When the navigation counts as done. Defaults to [Load](/api/E2E/GotoWaitUntil/#E2E_GotoWaitUntil_Load).

**Value:** [GotoWaitUntil](/api/E2E/GotoWaitUntil/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/GotoOptions.cs#L24)
