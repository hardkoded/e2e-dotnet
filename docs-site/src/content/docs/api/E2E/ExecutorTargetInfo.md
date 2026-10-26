---
title: "ExecutorTargetInfo"
description: "The target a step runs on. Tools scope themselves by Platform, and Verbs is the part of the action grammar the engine declared, so an executor offers a model..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorTargetInfo"
  hidden: true
---

<a id="E2E_ExecutorTargetInfo"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ExecutorTargetInfo
```


The target a step runs on. Tools scope themselves by [Platform](#E2E_ExecutorTargetInfo_Platform), and [Verbs](#E2E_ExecutorTargetInfo_Verbs) is the part of
the action grammar the engine declared, so an executor offers a model exactly the vocabulary the surface can honor.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L218)

## Properties

<a id="E2E_ExecutorTargetInfo_Name_"></a>
<a id="E2E_ExecutorTargetInfo_Name"></a>

### Name


```csharp
public required string Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L220)

<a id="E2E_ExecutorTargetInfo_Platform_"></a>
<a id="E2E_ExecutorTargetInfo_Platform"></a>

### Platform


```csharp
public required string Platform { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L222)

<a id="E2E_ExecutorTargetInfo_TapModifiers_"></a>
<a id="E2E_ExecutorTargetInfo_TapModifiers"></a>

### TapModifiers


```csharp
public bool TapModifiers { get; init; }
```


True when `CancellationToken)` and `CancellationToken)` take [Modifiers](/api/E2E/ClickOptions/#E2E_ClickOptions_Modifiers).

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L227)

<a id="E2E_ExecutorTargetInfo_Verbs_"></a>
<a id="E2E_ExecutorTargetInfo_Verbs"></a>

### Verbs


```csharp
public IReadOnlySet<ExecutorVerb> Verbs { get; init; }
```


**Value:** [IReadOnlySet&lt;ExecutorVerb&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlyset-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L224)
