---
title: "ProtocolManifest"
description: "The manifest: DescribeEngine derives one from an engine, CheckManifest checks the shape of a declared one, and ManifestMismatch compares the declared manifes..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProtocolManifest"
  hidden: true
---

<a id="E2E_Engine_Protocol_ProtocolManifest"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public static class ProtocolManifest
```


The manifest: `DescribeEngine` derives one from an engine, `CheckManifest` checks the shape of a declared one, and
`ManifestMismatch` compares the declared manifest with the one a server announces. Upstream's `engine/protocol/manifest.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

.NET-only holder name: C# has no free functions, and [EngineManifest](/api/E2E.Engine/EngineManifest/) already holds the engine manifest's.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Manifest.cs)

## Methods

<a id="E2E_Engine_Protocol_ProtocolManifest_CheckManifest_"></a>
<a id="E2E_Engine_Protocol_ProtocolManifest_CheckManifest_E2E_Engine_Protocol_EngineManifest_"></a>

### CheckManifest(EngineManifest)


```csharp
public static EngineManifest CheckManifest(EngineManifest manifest)
```


Checks a declared manifest's shape at config load: an object whose methods are protocol methods, each once. The rest is checked by
[DefineEngine(EngineSpec)](/api/E2E.Engine/EngineManifest/#E2E_Engine_EngineManifest_DefineEngine_E2E_Engine_EngineSpec_). Upstream's `checkManifest`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `manifest` | [EngineManifest](/api/E2E.Engine.Protocol/EngineManifest/) |  |

**Returns:** [EngineManifest](/api/E2E.Engine.Protocol/EngineManifest/)

<a id="E2E_Engine_Protocol_ProtocolManifest_DescribeEngine_"></a>
<a id="E2E_Engine_Protocol_ProtocolManifest_DescribeEngine_E2E_Engine_EngineHandle_"></a>

### DescribeEngine(EngineHandle)


```csharp
public static EngineManifest DescribeEngine(EngineHandle engine)
```


Describes an engine as the manifest `serveEngine` returns from `initialize`: its static members, the methods its declared
members answer, and the attempt hooks the server always answers. Upstream's `describeEngine`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `engine` | [EngineHandle](/api/E2E.Engine/EngineHandle/) |  |

**Returns:** [EngineManifest](/api/E2E.Engine.Protocol/EngineManifest/)

<a id="E2E_Engine_Protocol_ProtocolManifest_ManifestMismatch_"></a>
<a id="E2E_Engine_Protocol_ProtocolManifest_ManifestMismatch_E2E_Engine_Protocol_EngineManifest_E2E_Engine_Protocol_EngineManifest_"></a>

### ManifestMismatch(EngineManifest, EngineManifest?)


```csharp
public static string? ManifestMismatch(EngineManifest declared, EngineManifest? announced)
```


Names the fields where the server's manifest differs from the declared one, or null when they agree. Upstream's `manifestMismatch`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `declared` | [EngineManifest](/api/E2E.Engine.Protocol/EngineManifest/) |  |
| `announced` | [EngineManifest](/api/E2E.Engine.Protocol/EngineManifest/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)
