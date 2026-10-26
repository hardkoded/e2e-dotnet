---
title: "ExecutorSecret"
description: "A secret declared in a step's params: its stable name and what it is for. Never its value."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorSecret"
  hidden: true
---

<a id="E2E_ExecutorSecret"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExecutorSecret : IEquatable<ExecutorSecret>
```


A secret declared in a step's params: its stable name and what it is for. Never its value.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ExecutorSecret&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L179)

## Constructors

<a id="E2E_ExecutorSecret__ctor_"></a>
<a id="E2E_ExecutorSecret__ctor_System_String_System_String_"></a>

### ExecutorSecret(string, string?)


```csharp
public ExecutorSecret(string Name, string? Purpose)
```


A secret declared in a step's params: its stable name and what it is for. Never its value.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Purpose` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L179)

## Properties

<a id="E2E_ExecutorSecret_Name_"></a>
<a id="E2E_ExecutorSecret_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L179)

<a id="E2E_ExecutorSecret_Purpose_"></a>
<a id="E2E_ExecutorSecret_Purpose"></a>

### Purpose


```csharp
public string? Purpose { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L179)
