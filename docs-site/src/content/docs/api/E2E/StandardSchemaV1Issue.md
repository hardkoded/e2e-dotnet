---
title: "StandardSchemaV1Issue"
description: "Upstream's StandardSchemaV1.Issue: one problem with a value, and the path to it. .NET has no Standard Schema, so a schema is any StandardSchemaV1<T>."
editUrl: false
prev: false
next: false
sidebar:
  label: "StandardSchemaV1Issue"
  hidden: true
---

<a id="E2E_StandardSchemaV1Issue"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record StandardSchemaV1Issue : IEquatable<StandardSchemaV1Issue>
```


Upstream's `StandardSchemaV1.Issue`: one problem with a value, and the path to it. .NET has no Standard Schema, so a schema is any
`StandardSchemaV1&lt;T&gt;`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StandardSchemaV1Issue&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Values.cs)

## Constructors

<a id="E2E_StandardSchemaV1Issue__ctor_"></a>
<a id="E2E_StandardSchemaV1Issue__ctor_System_String_System_Collections_Generic_IReadOnlyList_System_Object__"></a>

### `StandardSchemaV1Issue(string, IReadOnlyList<object>?)`


```csharp
public StandardSchemaV1Issue(string Message, IReadOnlyList<object>? Path = null)
```


Upstream's `StandardSchemaV1.Issue`: one problem with a value, and the path to it. .NET has no Standard Schema, so a schema is any
`StandardSchemaV1&lt;T&gt;`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Message` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What is wrong, as the schema says it. |
| `Path` | [IReadOnlyList&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The keys and indexes from the root to the value, or `null` for an issue at the root. |

## Properties

<a id="E2E_StandardSchemaV1Issue_Message_"></a>
<a id="E2E_StandardSchemaV1Issue_Message"></a>

### Message


```csharp
public string Message { get; init; }
```


What is wrong, as the schema says it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_StandardSchemaV1Issue_Path_"></a>
<a id="E2E_StandardSchemaV1Issue_Path"></a>

### Path


```csharp
public IReadOnlyList<object>? Path { get; init; }
```


The keys and indexes from the root to the value, or `null` for an issue at the root.

**Value:** [IReadOnlyList&lt;object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
