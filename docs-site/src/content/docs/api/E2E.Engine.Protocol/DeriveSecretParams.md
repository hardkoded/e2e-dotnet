---
title: "DeriveSecretParams"
description: "Params of attempt/deriveSecret: the value the client resolved, for the forms the engine derives from it. Upstream's DeriveSecretParams."
editUrl: false
prev: false
next: false
sidebar:
  label: "DeriveSecretParams"
  hidden: true
---

<a id="E2E_Engine_Protocol_DeriveSecretParams"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record DeriveSecretParams : IEquatable<DeriveSecretParams>
```


Params of `attempt/deriveSecret`: the value the client resolved, for the forms the engine derives from it. Upstream's `DeriveSecretParams`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DeriveSecretParams&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_DeriveSecretParams__ctor_"></a>
<a id="E2E_Engine_Protocol_DeriveSecretParams__ctor_System_Int32_System_String_"></a>

### DeriveSecretParams(int, string)


```csharp
public DeriveSecretParams(int Derivation, string Plaintext)
```


Params of `attempt/deriveSecret`: the value the client resolved, for the forms the engine derives from it. Upstream's `DeriveSecretParams`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Derivation` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Plaintext` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Engine_Protocol_DeriveSecretParams_Derivation_"></a>
<a id="E2E_Engine_Protocol_DeriveSecretParams_Derivation"></a>

### Derivation


```csharp
public int Derivation { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_DeriveSecretParams_Plaintext_"></a>
<a id="E2E_Engine_Protocol_DeriveSecretParams_Plaintext"></a>

### Plaintext


```csharp
public string Plaintext { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
