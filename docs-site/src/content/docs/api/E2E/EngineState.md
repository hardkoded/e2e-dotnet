---
title: "EngineState"
description: "One captured engine state (upstream's EngineState, engine/surface.ts): an opaque, versioned payload an engine can save from a session and restore into anothe..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineState"
  hidden: true
---

<a id="E2E_EngineState"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record EngineState : IEquatable<EngineState>
```


One captured engine state (upstream's `EngineState`, `engine/surface.ts`): an opaque, versioned payload an engine can save
from a session and restore into another. `ExpiresAt` is an RFC 3339 timestamp after which the state must not be restored.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineState&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/EngineState.cs#L13)

## Constructors

<a id="E2E_EngineState__ctor_"></a>
<a id="E2E_EngineState__ctor_System_String_System_Int32_System_Text_Json_Nodes_JsonNode_System_String_"></a>

### EngineState(string, int, JsonNode?, string?)


```csharp
public EngineState(string Format, int Version, JsonNode? Data, string? ExpiresAt = null)
```


One captured engine state (upstream's `EngineState`, `engine/surface.ts`): an opaque, versioned payload an engine can save
from a session and restore into another. `ExpiresAt` is an RFC 3339 timestamp after which the state must not be restored.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Format` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Version` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Data` | [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode) |  |
| `ExpiresAt` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/EngineState.cs#L13)

## Properties

<a id="E2E_EngineState_Data_"></a>
<a id="E2E_EngineState_Data"></a>

### Data


```csharp
public JsonNode? Data { get; init; }
```


**Value:** [JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/EngineState.cs#L13)

<a id="E2E_EngineState_ExpiresAt_"></a>
<a id="E2E_EngineState_ExpiresAt"></a>

### ExpiresAt


```csharp
public string? ExpiresAt { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/EngineState.cs#L13)

<a id="E2E_EngineState_Format_"></a>
<a id="E2E_EngineState_Format"></a>

### Format


```csharp
public string Format { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/EngineState.cs#L13)

<a id="E2E_EngineState_Version_"></a>
<a id="E2E_EngineState_Version"></a>

### Version


```csharp
public int Version { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/EngineState.cs#L13)
