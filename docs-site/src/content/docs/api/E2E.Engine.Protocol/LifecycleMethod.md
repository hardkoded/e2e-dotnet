---
title: "LifecycleMethod"
description: "A method that carries one lifecycle hook, whose single argument is an info or cleanup object. Upstream's LifecycleMethod."
editUrl: false
prev: false
next: false
sidebar:
  label: "LifecycleMethod"
  hidden: true
---

<a id="E2E_Engine_Protocol_LifecycleMethod"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record LifecycleMethod : ProtocolMethod, IEquatable<ProtocolMethod>, IEquatable<LifecycleMethod>
```


A method that carries one lifecycle hook, whose single argument is an info or cleanup object. Upstream's `LifecycleMethod`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [ProtocolMethod](/api/E2E.Engine.Protocol/ProtocolMethod/)

**Implements:** [IEquatable&lt;ProtocolMethod&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LifecycleMethod&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_LifecycleMethod__ctor_"></a>
<a id="E2E_Engine_Protocol_LifecycleMethod__ctor_System_String_"></a>

### LifecycleMethod(string)


```csharp
public LifecycleMethod(string Member)
```


A method that carries one lifecycle hook, whose single argument is an info or cleanup object. Upstream's `LifecycleMethod`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Member` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Engine_Protocol_LifecycleMethod_Member_"></a>
<a id="E2E_Engine_Protocol_LifecycleMethod_Member"></a>

### Member


```csharp
public string Member { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
