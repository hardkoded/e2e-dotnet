---
title: "ExecutorTabs"
description: "The open tabs of a screen (upstream's ExecutorObservation.tabs), numbered from 1 in the order they opened."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorTabs"
  hidden: true
---

<a id="E2E_ExecutorTabs"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExecutorTabs : IEquatable<ExecutorTabs>
```


The open tabs of a screen (upstream's `ExecutorObservation.tabs`), numbered from 1 in the order they opened.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ExecutorTabs&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Constructors

<a id="E2E_ExecutorTabs__ctor_"></a>
<a id="E2E_ExecutorTabs__ctor_System_Int32_System_Int32_"></a>

### ExecutorTabs(int, int)


```csharp
public ExecutorTabs(int Count, int Active)
```


The open tabs of a screen (upstream's `ExecutorObservation.tabs`), numbered from 1 in the order they opened.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Count` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | How many tabs are open. |
| `Active` | [int](https://learn.microsoft.com/dotnet/api/system.int32) | The tab this screen shows, from 1. |

## Properties

<a id="E2E_ExecutorTabs_Active_"></a>
<a id="E2E_ExecutorTabs_Active"></a>

### Active


```csharp
public int Active { get; init; }
```


The tab this screen shows, from 1.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_ExecutorTabs_Count_"></a>
<a id="E2E_ExecutorTabs_Count"></a>

### Count


```csharp
public int Count { get; init; }
```


How many tabs are open.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
