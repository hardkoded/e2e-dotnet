---
title: "ObservationLimits"
description: "Limits from the engine contract. Engines cut names and text before they reach the model."
editUrl: false
prev: false
next: false
sidebar:
  label: "ObservationLimits"
  hidden: true
---

<a id="E2E_Engine_ObservationLimits"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public static class ObservationLimits
```


Limits from the engine contract. Engines cut names and text before they reach the model.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L8)

## Fields

<a id="E2E_Engine_ObservationLimits_Name"></a>

### Name


```csharp
public const int Name = 256
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L10)

<a id="E2E_Engine_ObservationLimits_Nodes"></a>

### Nodes


```csharp
public const int Nodes = 3000
```


Most nodes one web observation lists, across every frame of the page.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L14)

<a id="E2E_Engine_ObservationLimits_Text"></a>

### Text


```csharp
public const int Text = 512
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Observation.cs#L11)
