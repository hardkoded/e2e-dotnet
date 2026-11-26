---
title: "OperationMethod"
description: "A method that carries one engine operation: the member it calls and that member's arguments by name, in order. Upstream's OperationMethod."
editUrl: false
prev: false
next: false
sidebar:
  label: "OperationMethod"
  hidden: true
---

<a id="E2E_Engine_Protocol_OperationMethod"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record OperationMethod : ProtocolMethod, IEquatable<ProtocolMethod>, IEquatable<OperationMethod>
```


A method that carries one engine operation: the member it calls and that member's arguments by name, in order. Upstream's `OperationMethod`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ProtocolMethod](/api/E2E.Engine.Protocol/ProtocolMethod/)

**Implements:** [IEquatable&lt;ProtocolMethod&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;OperationMethod&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_OperationMethod__ctor_"></a>
<a id="E2E_Engine_Protocol_OperationMethod__ctor_System_String_System_Collections_Generic_IReadOnlyList_System_String__System_String_"></a>

### `OperationMethod(string, IReadOnlyList<string>, string?)`


```csharp
public OperationMethod(string Member, IReadOnlyList<string> Args, string? Result = null)
```


A method that carries one engine operation: the member it calls and that member's arguments by name, in order. Upstream's `OperationMethod`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Member` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The engine member: a top-level one (`observe`) or a hook of a nested one (`keyboard.type`). |
| `Args` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The member's arguments, named as the params carry them; `operation` is its operation context. |
| `Result` | [string](https://learn.microsoft.com/dotnet/api/system.string) | A result that is not plain JSON: a snapshot's pixels cross as base64, a screenshot may cross as its bytes. |

## Properties

<a id="E2E_Engine_Protocol_OperationMethod_Args_"></a>
<a id="E2E_Engine_Protocol_OperationMethod_Args"></a>

### Args


```csharp
public IReadOnlyList<string> Args { get; init; }
```


The member's arguments, named as the params carry them; `operation` is its operation context.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_Protocol_OperationMethod_Member_"></a>
<a id="E2E_Engine_Protocol_OperationMethod_Member"></a>

### Member


```csharp
public string Member { get; init; }
```


The engine member: a top-level one (`observe`) or a hook of a nested one (`keyboard.type`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_OperationMethod_Result_"></a>
<a id="E2E_Engine_Protocol_OperationMethod_Result"></a>

### Result


```csharp
public string? Result { get; init; }
```


A result that is not plain JSON: a snapshot's pixels cross as base64, a screenshot may cross as its bytes.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
