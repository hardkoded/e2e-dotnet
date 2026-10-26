---
title: "StepScreen"
description: "How the screen a step saw differs from the one before (upstream's StepScreen)."
editUrl: false
prev: false
next: false
sidebar:
  label: "StepScreen"
  hidden: true
---

<a id="E2E_Reports_StepScreen"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record StepScreen : IEquatable<StepScreen>
```


How the screen a step saw differs from the one before (upstream's `StepScreen`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StepScreen&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L639)

## Properties

<a id="E2E_Reports_StepScreen_Changes_"></a>
<a id="E2E_Reports_StepScreen_Changes"></a>

### Changes


```csharp
public IReadOnlyList<string> Changes { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L647)

<a id="E2E_Reports_StepScreen_Location_"></a>
<a id="E2E_Reports_StepScreen_Location"></a>

### Location


```csharp
public string? Location { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L641)

<a id="E2E_Reports_StepScreen_More_"></a>
<a id="E2E_Reports_StepScreen_More"></a>

### More


```csharp
public int? More { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L649)

<a id="E2E_Reports_StepScreen_Nodes_"></a>
<a id="E2E_Reports_StepScreen_Nodes"></a>

### Nodes


```csharp
public int Nodes { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L643)

<a id="E2E_Reports_StepScreen_Since_"></a>
<a id="E2E_Reports_StepScreen_Since"></a>

### Since


```csharp
public int? Since { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Report.cs#L645)
