---
title: "StandardSchemaV1Result<TOutput>"
description: "Upstream's StandardSchemaV1.Result: the output when the value matches, or the issues when it does not. Issues is null on a match."
editUrl: false
prev: false
next: false
sidebar:
  label: "StandardSchemaV1Result<TOutput>"
  hidden: true
---

<a id="E2E_StandardSchemaV1Result_1"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record StandardSchemaV1Result<TOutput> : IEquatable<StandardSchemaV1Result<TOutput>>
```


Upstream's `StandardSchemaV1.Result`: the output when the value matches, or the issues when it does not. `Issues` is
`null` on a match.

**Type parameters**

| Name | Description |
| --- | --- |
| `TOutput` |  |

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;StandardSchemaV1Result&lt;TOutput&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Values.cs)

## Constructors

<a id="E2E_StandardSchemaV1Result_1__ctor_"></a>
<a id="E2E_StandardSchemaV1Result_1__ctor__0_System_Collections_Generic_IReadOnlyList_E2E_StandardSchemaV1Issue__"></a>

### `StandardSchemaV1Result(TOutput?, IReadOnlyList<StandardSchemaV1Issue>?)`


```csharp
public StandardSchemaV1Result(TOutput? Value, IReadOnlyList<StandardSchemaV1Issue>? Issues)
```


Upstream's `StandardSchemaV1.Result`: the output when the value matches, or the issues when it does not. `Issues` is
`null` on a match.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Value` | `TOutput` |  |
| `Issues` | [IReadOnlyList&lt;StandardSchemaV1Issue&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_StandardSchemaV1Result_1_Issues_"></a>
<a id="E2E_StandardSchemaV1Result_1_Issues"></a>

### Issues


```csharp
public IReadOnlyList<StandardSchemaV1Issue>? Issues { get; init; }
```


**Value:** [IReadOnlyList&lt;StandardSchemaV1Issue&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_StandardSchemaV1Result_1_Value_"></a>
<a id="E2E_StandardSchemaV1Result_1_Value"></a>

### Value


```csharp
public TOutput? Value { get; init; }
```


**Value:** `TOutput`

## Methods

<a id="E2E_StandardSchemaV1Result_1_Failure_"></a>
<a id="E2E_StandardSchemaV1Result_1_Failure_System_Collections_Generic_IReadOnlyList_E2E_StandardSchemaV1Issue__"></a>

### `Failure(IReadOnlyList<StandardSchemaV1Issue>)`


```csharp
public static StandardSchemaV1Result<TOutput> Failure(IReadOnlyList<StandardSchemaV1Issue> issues)
```


A mismatch, with every issue found.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `issues` | [IReadOnlyList&lt;StandardSchemaV1Issue&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

**Returns:** [StandardSchemaV1Result&lt;TOutput&gt;](./)

<a id="E2E_StandardSchemaV1Result_1_Success_"></a>
<a id="E2E_StandardSchemaV1Result_1_Success__0_"></a>

### Success(TOutput)


```csharp
public static StandardSchemaV1Result<TOutput> Success(TOutput value)
```


A match, with the output to return.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | `TOutput` |  |

**Returns:** [StandardSchemaV1Result&lt;TOutput&gt;](./)
