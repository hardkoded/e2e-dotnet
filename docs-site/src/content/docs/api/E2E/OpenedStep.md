---
title: "OpenedStep"
description: "A step as opened: title and charter bounded to the report's ceilings, which is what the step runs with."
editUrl: false
prev: false
next: false
sidebar:
  label: "OpenedStep"
  hidden: true
---

<a id="E2E_OpenedStep"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record OpenedStep : IEquatable<OpenedStep>
```


A step as opened: title and charter bounded to the report's ceilings, which is what the step runs with.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;OpenedStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L16)

## Constructors

<a id="E2E_OpenedStep__ctor_"></a>
<a id="E2E_OpenedStep__ctor_System_Int32_System_String_System_String_"></a>

### OpenedStep(int, string, string)


```csharp
public OpenedStep(int Index, string Title, string Instruction)
```


A step as opened: title and charter bounded to the report's ceilings, which is what the step runs with.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Index` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | One-based position of the step. |
| `Title` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The area or flow, at most 200 characters. |
| `Instruction` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The charter the agent runs, at most 2000 characters. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L16)

## Properties

<a id="E2E_OpenedStep_Index_"></a>
<a id="E2E_OpenedStep_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


One-based position of the step.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L16)

<a id="E2E_OpenedStep_Instruction_"></a>
<a id="E2E_OpenedStep_Instruction"></a>

### Instruction


```csharp
public string Instruction { get; init; }
```


The charter the agent runs, at most 2000 characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L16)

<a id="E2E_OpenedStep_Title_"></a>
<a id="E2E_OpenedStep_Title"></a>

### Title


```csharp
public string Title { get; init; }
```


The area or flow, at most 200 characters.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Explore/Progress.cs#L16)
