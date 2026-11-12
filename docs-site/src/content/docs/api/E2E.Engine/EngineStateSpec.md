---
title: "EngineStateSpec"
description: "Upstream's state hooks of an engine: capturing and restoring the app's state."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineStateSpec"
  hidden: true
---

<a id="E2E_Engine_EngineStateSpec"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineStateSpec : IEquatable<EngineStateSpec>
```


Upstream's `state` hooks of an engine: capturing and restoring the app's state.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineStateSpec&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Manifest.cs)

## Properties

<a id="E2E_Engine_EngineStateSpec_Capture_"></a>
<a id="E2E_Engine_EngineStateSpec_Capture"></a>

### Capture


```csharp
public Func<OperationContext, Task<EngineState>>? Capture { get; init; }
```


**Value:** [Func&lt;OperationContext, Task&lt;EngineState&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineStateSpec_Restore_"></a>
<a id="E2E_Engine_EngineStateSpec_Restore"></a>

### Restore


```csharp
public Func<EngineState, OperationContext, Task>? Restore { get; init; }
```


**Value:** [Func&lt;EngineState, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)
