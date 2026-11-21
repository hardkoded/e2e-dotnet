---
title: "SpaceObservation"
description: "What the action space reads of the screen (upstream's SpaceObservation)."
editUrl: false
prev: false
next: false
sidebar:
  label: "SpaceObservation"
  hidden: true
---

<a id="E2E_Decision_SpaceObservation"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class SpaceObservation
```


What the action space reads of the screen (upstream's `SpaceObservation`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Elements.cs)

## Properties

<a id="E2E_Decision_SpaceObservation_KeyboardVisible_"></a>
<a id="E2E_Decision_SpaceObservation_KeyboardVisible"></a>

### KeyboardVisible


```csharp
public bool? KeyboardVisible { get; init; }
```


The engine's measured keyboard state; null leaves it to the tree.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_SpaceObservation_Locates_"></a>
<a id="E2E_Decision_SpaceObservation_Locates"></a>

### Locates


```csharp
public bool Locates { get; init; }
```


Whether the step can locate a drawn control on the pixels: the decision model scores and a text model names it.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_SpaceObservation_Path_"></a>
<a id="E2E_Decision_SpaceObservation_Path"></a>

### Path


```csharp
public string? Path { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_SpaceObservation_Pixels_"></a>
<a id="E2E_Decision_SpaceObservation_Pixels"></a>

### Pixels


```csharp
public ExecutorPixels? Pixels { get; init; }
```


Granted pixels; with the `tapAt` verb and a step that locates they open `tap_at`.

**Value:** [ExecutorPixels](/api/E2E/ExecutorPixels/)

<a id="E2E_Decision_SpaceObservation_Tree_"></a>
<a id="E2E_Decision_SpaceObservation_Tree"></a>

### Tree


```csharp
public required ExecutorNode Tree { get; init; }
```


The tree root. Upstream's action space leaves it out of pointer operations.

**Value:** [ExecutorNode](/api/E2E/ExecutorNode/)

<a id="E2E_Decision_SpaceObservation_Viewport_"></a>
<a id="E2E_Decision_SpaceObservation_Viewport"></a>

### Viewport


```csharp
public required TraceViewport Viewport { get; init; }
```


**Value:** [TraceViewport](/api/E2E.Engine/TraceViewport/)
