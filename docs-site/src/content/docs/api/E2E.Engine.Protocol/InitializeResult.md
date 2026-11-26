---
title: "InitializeResult"
description: "Result of initialize. Upstream's InitializeResult."
editUrl: false
prev: false
next: false
sidebar:
  label: "InitializeResult"
  hidden: true
---

<a id="E2E_Engine_Protocol_InitializeResult"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record InitializeResult : IEquatable<InitializeResult>
```


Result of `initialize`. Upstream's `InitializeResult`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;InitializeResult&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_InitializeResult__ctor_"></a>
<a id="E2E_Engine_Protocol_InitializeResult__ctor_System_Int32_E2E_Engine_Protocol_EngineManifest_"></a>

### InitializeResult(int, EngineManifest)


```csharp
public InitializeResult(int ProtocolVersion, EngineManifest Manifest)
```


Result of `initialize`. Upstream's `InitializeResult`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `ProtocolVersion` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Manifest` | [EngineManifest](/api/E2E.Engine.Protocol/EngineManifest/) |  |

## Properties

<a id="E2E_Engine_Protocol_InitializeResult_Manifest_"></a>
<a id="E2E_Engine_Protocol_InitializeResult_Manifest"></a>

### Manifest


```csharp
public EngineManifest Manifest { get; init; }
```


**Value:** [EngineManifest](/api/E2E.Engine.Protocol/EngineManifest/)

<a id="E2E_Engine_Protocol_InitializeResult_ProtocolVersion_"></a>
<a id="E2E_Engine_Protocol_InitializeResult_ProtocolVersion"></a>

### ProtocolVersion


```csharp
public int ProtocolVersion { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)
