---
title: "EngineManifest"
description: "What an engine server is, as JSON: the static members of an engine and the methods it answers. The client declares it at config load, where the harness needs..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineManifest"
  hidden: true
---

<a id="E2E_Engine_Protocol_EngineManifest"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record EngineManifest : IEquatable<EngineManifest>
```


What an engine server is, as JSON: the static members of an engine and the methods it answers. The client declares it at config load,
where the harness needs an engine's capabilities synchronously, and the server returns its own from `initialize`; the two must
agree. Upstream's `EngineManifest` (`engine/protocol/protocol.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineManifest&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Manifest.cs)

## Constructors

<a id="E2E_Engine_Protocol_EngineManifest__ctor_"></a>
<a id="E2E_Engine_Protocol_EngineManifest__ctor_System_String_System_String_System_Int32_System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `EngineManifest(string, string, int, IReadOnlyList<string>)`


```csharp
public EngineManifest(string Name, string Version, int SpiVersion, IReadOnlyList<string> Methods)
```


What an engine server is, as JSON: the static members of an engine and the methods it answers. The client declares it at config load,
where the harness needs an engine's capabilities synchronously, and the server returns its own from `initialize`; the two must
agree. Upstream's `EngineManifest` (`engine/protocol/protocol.ts`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Version` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `SpiVersion` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `Methods` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_Engine_Protocol_EngineManifest_Actions_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_Actions"></a>

### Actions


```csharp
public IReadOnlyList<string>? Actions { get; init; }
```


Required with `engine/perform`; see [Actions](/api/E2E.Engine/EngineHandle/#E2E_Engine_EngineHandle_Actions).

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_Protocol_EngineManifest_FileApps_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_FileApps"></a>

### FileApps


```csharp
public bool? FileApps { get; init; }
```


See [FileApps](/api/E2E.Engine/EngineHandle/#E2E_Engine_EngineHandle_FileApps).

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_Protocol_EngineManifest_GrantsAllPermissions_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_GrantsAllPermissions"></a>

### GrantsAllPermissions


```csharp
public bool? GrantsAllPermissions { get; init; }
```


See [GrantsAllPermissions](/api/E2E.Engine/EngineHandle/#E2E_Engine_EngineHandle_GrantsAllPermissions).

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_Protocol_EngineManifest_Isolation_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_Isolation"></a>

### Isolation


```csharp
public string? Isolation { get; init; }
```


See [Isolation](/api/E2E.Engine/EngineHandle/#E2E_Engine_EngineHandle_Isolation); left out for `attempt`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_EngineManifest_LaunchArgumentValues_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_LaunchArgumentValues"></a>

### LaunchArgumentValues


```csharp
public bool? LaunchArgumentValues { get; init; }
```


See [LaunchArgumentValues](/api/E2E.Engine/EngineHandle/#E2E_Engine_EngineHandle_LaunchArgumentValues).

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_Protocol_EngineManifest_Methods_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_Methods"></a>

### Methods


```csharp
public IReadOnlyList<string> Methods { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_Protocol_EngineManifest_Name_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_EngineManifest_Platform_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_Platform"></a>

### Platform


```csharp
public string? Platform { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_EngineManifest_PointerActions_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_PointerActions"></a>

### PointerActions


```csharp
public IReadOnlyList<string>? PointerActions { get; init; }
```


Required with `engine/performAt`; see [PointerActions](/api/E2E.Engine/EngineHandle/#E2E_Engine_EngineHandle_PointerActions).

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_Protocol_EngineManifest_SelectsDevice_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_SelectsDevice"></a>

### SelectsDevice


```csharp
public bool? SelectsDevice { get; init; }
```


See [SelectsDevice](/api/E2E.Engine/EngineHandle/#E2E_Engine_EngineHandle_SelectsDevice).

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_Protocol_EngineManifest_SpiVersion_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_SpiVersion"></a>

### SpiVersion


```csharp
public int SpiVersion { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_Protocol_EngineManifest_TapModifiers_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_TapModifiers"></a>

### TapModifiers


```csharp
public bool? TapModifiers { get; init; }
```


See [TapModifiers](/api/E2E.Engine/EngineHandle/#E2E_Engine_EngineHandle_TapModifiers).

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_Protocol_EngineManifest_Version_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_Version"></a>

### Version


```csharp
public string Version { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_Protocol_EngineManifest_Workers_"></a>
<a id="E2E_Engine_Protocol_EngineManifest_Workers"></a>

### Workers


```csharp
public int? Workers { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)
