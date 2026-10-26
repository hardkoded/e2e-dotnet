---
title: "ExecutorNode"
description: "One node of the redacted semantic tree. Names, text, values, and attribute values are secret-redacted, and a secure node carries no value at all. Id is the n..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorNode"
  hidden: true
---

<a id="E2E_ExecutorNode"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ExecutorNode
```


One node of the redacted semantic tree. Names, text, values, and attribute values are secret-redacted, and a secure
node carries no value at all. [Id](#E2E_ExecutorNode_Id) is the node's ref in the observation, which an action names it by.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L266)

## Properties

<a id="E2E_ExecutorNode_Attributes_"></a>
<a id="E2E_ExecutorNode_Attributes"></a>

### Attributes


```csharp
public IReadOnlyDictionary<string, string>? Attributes { get; init; }
```


**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L284)

<a id="E2E_ExecutorNode_Children_"></a>
<a id="E2E_ExecutorNode_Children"></a>

### Children


```csharp
public IReadOnlyList<ExecutorNode> Children { get; init; }
```


**Value:** [IReadOnlyList&lt;ExecutorNode&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L288)

<a id="E2E_ExecutorNode_Id_"></a>
<a id="E2E_ExecutorNode_Id"></a>

### Id


```csharp
public required string Id { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L268)

<a id="E2E_ExecutorNode_InputPurpose_"></a>
<a id="E2E_ExecutorNode_InputPurpose"></a>

### InputPurpose


```csharp
public string? InputPurpose { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L280)

<a id="E2E_ExecutorNode_Name_"></a>
<a id="E2E_ExecutorNode_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L272)

<a id="E2E_ExecutorNode_Rect_"></a>
<a id="E2E_ExecutorNode_Rect"></a>

### Rect


```csharp
public BoundingBox? Rect { get; init; }
```


**Value:** [BoundingBox](/api/E2E.Engine/BoundingBox/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L286)

<a id="E2E_ExecutorNode_Role_"></a>
<a id="E2E_ExecutorNode_Role"></a>

### Role


```csharp
public string? Role { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L270)

<a id="E2E_ExecutorNode_Selection_"></a>
<a id="E2E_ExecutorNode_Selection"></a>

### Selection


```csharp
public string? Selection { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L278)

<a id="E2E_ExecutorNode_States_"></a>
<a id="E2E_ExecutorNode_States"></a>

### States


```csharp
public NodeStates States { get; init; }
```


**Value:** [NodeStates](/api/E2E.Engine/NodeStates/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L282)

<a id="E2E_ExecutorNode_Text_"></a>
<a id="E2E_ExecutorNode_Text"></a>

### Text


```csharp
public string? Text { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L274)

<a id="E2E_ExecutorNode_Value_"></a>
<a id="E2E_ExecutorNode_Value"></a>

### Value


```csharp
public string? Value { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L276)
