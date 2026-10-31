---
title: "ExecutorObserveOptions"
description: "What an executor asks CancellationToken) to include beyond the text."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorObserveOptions"
  hidden: true
---

<a id="E2E_ExecutorObserveOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ExecutorObserveOptions
```


What an executor asks `CancellationToken)` to include beyond the text.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L249)

## Properties

<a id="E2E_ExecutorObserveOptions_Pixels_"></a>
<a id="E2E_ExecutorObserveOptions_Pixels"></a>

### Pixels


```csharp
public bool Pixels { get; init; }
```


Includes the masked pixels as [Pixels](/api/E2E/ExecutorObservation/#E2E_ExecutorObservation_Pixels). They are granted only when the engine
captures pixels, and its masking is proven. Otherwise
[PixelsWithheld](/api/E2E/ExecutorObservation/#E2E_ExecutorObservation_PixelsWithheld) says why.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L259)

<a id="E2E_ExecutorObserveOptions_Tree_"></a>
<a id="E2E_ExecutorObserveOptions_Tree"></a>

### Tree


```csharp
public bool Tree { get; init; }
```


Includes the redacted node tree as [Tree](/api/E2E/ExecutorObservation/#E2E_ExecutorObservation_Tree).

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L252)
