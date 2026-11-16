---
title: "ExecutorReadResult"
description: "What ReadAsync measured on a node (upstream's ExecutorReadResult)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorReadResult"
  hidden: true
---

<a id="E2E_ExecutorReadResult"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ExecutorReadResult : IEquatable<ExecutorReadResult>
```


What `ReadAsync` measured on a node (upstream's `ExecutorReadResult`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ExecutorReadResult&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Constructors

<a id="E2E_ExecutorReadResult__ctor_"></a>
<a id="E2E_ExecutorReadResult__ctor_System_Collections_Generic_IReadOnlyDictionary_System_String_System_String__"></a>

### `ExecutorReadResult(IReadOnlyDictionary<string, string>)`


```csharp
public ExecutorReadResult(IReadOnlyDictionary<string, string> Style)
```


What `ReadAsync` measured on a node (upstream's `ExecutorReadResult`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Style` | [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) | Only the requested properties, as computed strings with secret values redacted. |

## Properties

<a id="E2E_ExecutorReadResult_Style_"></a>
<a id="E2E_ExecutorReadResult_Style"></a>

### Style


```csharp
public IReadOnlyDictionary<string, string> Style { get; init; }
```


Only the requested properties, as computed strings with secret values redacted.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
