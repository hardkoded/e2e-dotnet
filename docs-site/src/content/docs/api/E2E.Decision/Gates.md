---
title: "Gates"
description: "The gates every answer passes: the selected option's probability and the provider's confidence (upstream's Gates)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Gates"
  hidden: true
---

<a id="E2E_Decision_Gates"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record Gates : IEquatable<Gates>
```


The gates every answer passes: the selected option's probability and the provider's confidence (upstream's `Gates`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;Gates&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Picks.cs)

## Constructors

<a id="E2E_Decision_Gates__ctor_"></a>
<a id="E2E_Decision_Gates__ctor_System_Double_System_Double_"></a>

### Gates(double, double)


```csharp
public Gates(double MinProbability, double MinConfidence)
```


The gates every answer passes: the selected option's probability and the provider's confidence (upstream's `Gates`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `MinProbability` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `MinConfidence` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

## Properties

<a id="E2E_Decision_Gates_MinConfidence_"></a>
<a id="E2E_Decision_Gates_MinConfidence"></a>

### MinConfidence


```csharp
public double MinConfidence { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Gates_MinProbability_"></a>
<a id="E2E_Decision_Gates_MinProbability"></a>

### MinProbability


```csharp
public double MinProbability { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)
