---
title: "ExecutorObservation"
description: "A redacted, size-bounded observation an executor may show its model."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorObservation"
  hidden: true
---

<a id="E2E_ExecutorObservation"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExecutorObservation : IEquatable<ExecutorObservation>
```


A redacted, size-bounded observation an executor may show its model.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ExecutorObservation&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L302)

## Constructors

<a id="E2E_ExecutorObservation__ctor_"></a>
<a id="E2E_ExecutorObservation__ctor_System_String_"></a>

### ExecutorObservation(string)


```csharp
public ExecutorObservation(string Text)
```


A redacted, size-bounded observation an executor may show its model.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The screen as the model reads it, already secret-redacted. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L302)

## Properties

<a id="E2E_ExecutorObservation_KeyboardVisible_"></a>
<a id="E2E_ExecutorObservation_KeyboardVisible"></a>

### KeyboardVisible


```csharp
public bool? KeyboardVisible { get; init; }
```


Whether an on-screen keyboard is showing, as the engine measured it apart from the tree; false when it measured none.
Null when the engine did not measure it (the web engine never does): the tree's `keyboard` and `key` nodes, or a screenshot, are then the evidence.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L314)

<a id="E2E_ExecutorObservation_Path_"></a>
<a id="E2E_ExecutorObservation_Path"></a>

### Path


```csharp
public string? Path { get; init; }
```


The location of the screen, redacted, when the engine reports one. Null on an engine without one.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L317)

<a id="E2E_ExecutorObservation_Pixels_"></a>
<a id="E2E_ExecutorObservation_Pixels"></a>

### Pixels


```csharp
public ExecutorPixels? Pixels { get; init; }
```


The masked pixels. Present when the call asked for them with [Pixels](/api/E2E/ExecutorObserveOptions/#E2E_ExecutorObserveOptions_Pixels) and they were granted.

**Value:** [ExecutorPixels](/api/E2E/ExecutorPixels/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L323)

<a id="E2E_ExecutorObservation_PixelsWithheld_"></a>
<a id="E2E_ExecutorObservation_PixelsWithheld"></a>

### PixelsWithheld


```csharp
public VisionDegradation? PixelsWithheld { get; init; }
```


Why requested pixels were withheld.

**Value:** `VisionDegradation?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L326)

<a id="E2E_ExecutorObservation_Text_"></a>
<a id="E2E_ExecutorObservation_Text"></a>

### Text


```csharp
public string Text { get; init; }
```


The screen as the model reads it, already secret-redacted.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L302)

<a id="E2E_ExecutorObservation_Tree_"></a>
<a id="E2E_ExecutorObservation_Tree"></a>

### Tree


```csharp
public IReadOnlyList<ExecutorNode>? Tree { get; init; }
```


The redacted node tree. Present when the call asked for it with [Tree](/api/E2E/ExecutorObserveOptions/#E2E_ExecutorObserveOptions_Tree).

**Value:** [IReadOnlyList&lt;ExecutorNode&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L320)

<a id="E2E_ExecutorObservation_TreeUnavailable_"></a>
<a id="E2E_ExecutorObservation_TreeUnavailable"></a>

### TreeUnavailable


```csharp
public bool TreeUnavailable { get; init; }
```


Semantic capture failed: use the accompanying pixels, and never read the listing as evidence of absence.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L305)

<a id="E2E_ExecutorObservation_Truncated_"></a>
<a id="E2E_ExecutorObservation_Truncated"></a>

### Truncated


```csharp
public bool Truncated { get; init; }
```


True when the engine left nodes out to stay within its budget. Scrolling brings them in.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L308)
