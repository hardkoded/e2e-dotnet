---
title: "ExecutorTarget"
description: "A node named by its id from the newest observation (upstream's { id }). The id is the node's ref in the text and in Id. A ref the screen no longer lists fail..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorTarget"
  hidden: true
---

<a id="E2E_ExecutorTarget"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ExecutorTarget
```


A node named by its id from the newest observation (upstream's `{ id }`). The id is the node's ref in the text and in
[Id](/api/E2E/ExecutorNode/#E2E_ExecutorNode_Id). A ref the screen no longer lists fails the action instead of acting on the wrong node.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L336)

## Constructors

<a id="E2E_ExecutorTarget__ctor_"></a>
<a id="E2E_ExecutorTarget__ctor_System_String_"></a>

### ExecutorTarget(string)


```csharp
public ExecutorTarget(string id)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `id` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L338)

## Properties

<a id="E2E_ExecutorTarget_Id_"></a>
<a id="E2E_ExecutorTarget_Id"></a>

### Id


```csharp
public string Id { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L350)
