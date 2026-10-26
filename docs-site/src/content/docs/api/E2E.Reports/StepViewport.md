---
title: "StepViewport"
description: "The viewport a step ran in."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepViewport"
  hidden: true
---

<a id="E2E_Reports_StepViewport"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record StepViewport : IEquatable<StepViewport>
```


The viewport a step ran in.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StepViewport&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L613)

## Properties

<a id="E2E_Reports_StepViewport_Height_"></a>
<a id="E2E_Reports_StepViewport_Height"></a>

### Height


```csharp
public int Height { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L617)

<a id="E2E_Reports_StepViewport_Scale_"></a>
<a id="E2E_Reports_StepViewport_Scale"></a>

### Scale


```csharp
public double Scale { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L619)

<a id="E2E_Reports_StepViewport_Width_"></a>
<a id="E2E_Reports_StepViewport_Width"></a>

### Width


```csharp
public int Width { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L615)
