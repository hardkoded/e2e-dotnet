---
title: "EngineSessionSpec"
description: "Upstream's session hooks of an engine: opening, navigating back, resizing, switching and closing tabs, restarting, and resetting the app."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineSessionSpec"
  hidden: true
---

<a id="E2E_Engine_EngineSessionSpec"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineSessionSpec : IEquatable<EngineSessionSpec>
```


Upstream's `session` hooks of an engine: opening, navigating back, resizing, switching and closing tabs, restarting, and resetting the app.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineSessionSpec&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Manifest.cs)

## Properties

<a id="E2E_Engine_EngineSessionSpec_Back_"></a>
<a id="E2E_Engine_EngineSessionSpec_Back"></a>

### Back


```csharp
public Func<OperationContext, Task>? Back { get; init; }
```


**Value:** [Func&lt;OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineSessionSpec_CloseTab_"></a>
<a id="E2E_Engine_EngineSessionSpec_CloseTab"></a>

### CloseTab


```csharp
public Func<int, OperationContext, Task>? CloseTab { get; init; }
```


Closes the open tab at a zero-based index, counted as [SwitchTab](#E2E_Engine_EngineSessionSpec_SwitchTab) counts them. When it was the tab shown, the engine shows
another, such as the tab that opened it. An index with no open tab, or the last open tab, is an `ACTION_FAILED` the model reads.

**Value:** [Func&lt;int, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Engine_EngineSessionSpec_Open_"></a>
<a id="E2E_Engine_EngineSessionSpec_Open"></a>

### Open


```csharp
public Func<string, OperationContext, Task>? Open { get; init; }
```


**Value:** [Func&lt;string, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Engine_EngineSessionSpec_Reset_"></a>
<a id="E2E_Engine_EngineSessionSpec_Reset"></a>

### Reset


```csharp
public Func<OperationContext, Task>? Reset { get; init; }
```


**Value:** [Func&lt;OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineSessionSpec_Resize_"></a>
<a id="E2E_Engine_EngineSessionSpec_Resize"></a>

### Resize


```csharp
public Func<ViewportSize, OperationContext, Task>? Resize { get; init; }
```


**Value:** [Func&lt;ViewportSize, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Engine_EngineSessionSpec_Restart_"></a>
<a id="E2E_Engine_EngineSessionSpec_Restart"></a>

### Restart


```csharp
public Func<OperationContext, Task>? Restart { get; init; }
```


**Value:** [Func&lt;OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineSessionSpec_SwitchTab_"></a>
<a id="E2E_Engine_EngineSessionSpec_SwitchTab"></a>

### SwitchTab


```csharp
public Func<int, OperationContext, Task>? SwitchTab { get; init; }
```


Shows the open tab at a zero-based index, in the order the tabs opened, as [Tabs](/api/E2E.Engine/EngineSnapshot/#E2E_Engine_EngineSnapshot_Tabs) counts them. An index with
no open tab is an `ACTION_FAILED` the model reads.

**Value:** [Func&lt;int, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)
