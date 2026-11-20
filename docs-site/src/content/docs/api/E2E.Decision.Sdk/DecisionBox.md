---
title: "DecisionBox"
description: "Edges in [0, 1], measured from the image's top-left corner."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionBox"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionBox"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DecisionBox : IEquatable<DecisionBox>
```


Edges in [0, 1], measured from the image's top-left corner.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DecisionBox&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionBox__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionBox__ctor_System_Double_System_Double_System_Double_System_Double_"></a>

### DecisionBox(double, double, double, double)


```csharp
public DecisionBox(double Top, double Bottom, double Left, double Right)
```


Edges in [0, 1], measured from the image's top-left corner.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Top` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `Bottom` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `Left` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |
| `Right` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionBox_Bottom_"></a>
<a id="E2E_Decision_Sdk_DecisionBox_Bottom"></a>

### Bottom


```csharp
public double Bottom { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_DecisionBox_Left_"></a>
<a id="E2E_Decision_Sdk_DecisionBox_Left"></a>

### Left


```csharp
public double Left { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_DecisionBox_Right_"></a>
<a id="E2E_Decision_Sdk_DecisionBox_Right"></a>

### Right


```csharp
public double Right { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_Decision_Sdk_DecisionBox_Top_"></a>
<a id="E2E_Decision_Sdk_DecisionBox_Top"></a>

### Top


```csharp
public double Top { get; init; }
```


**Value:** [double](https://learn.microsoft.com/dotnet/api/system.double)
