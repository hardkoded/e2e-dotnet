---
title: "TraceKeyContext"
description: "Upstream's TraceKeyContext (cache/trace.ts): every part of a cache key outside the step's own identity, as it was when a step was recorded. An entry stores i..."
editUrl: false
prev: false
next: false
sidebar:
  label: "TraceKeyContext"
  hidden: true
---

<a id="E2E_TraceKeyContext"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record TraceKeyContext : IEquatable<TraceKeyContext>
```


Upstream's `TraceKeyContext` (`cache/trace.ts`): every part of a cache key outside the step's own identity, as it was
when a step was recorded. An entry stores it as `keyedBy`: provenance, never read by a replay. When a step's key no longer
finds the entry, comparing it with the run's says what changed.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;TraceKeyContext&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/Trace.cs#L15)

## Constructors

<a id="E2E_TraceKeyContext__ctor_"></a>
<a id="E2E_TraceKeyContext__ctor_System_String_System_String_System_String_System_String_System_String_System_Int32_System_String_System_String_"></a>

### TraceKeyContext(string, string, string, string, string, int, string, string)


```csharp
public TraceKeyContext(string CacheSchema, string PolicyVersion, string Project, string Platform, string EngineName, int EngineSpiVersion, string AppIdentity, string AgentContextDigest)
```


Upstream's `TraceKeyContext` (`cache/trace.ts`): every part of a cache key outside the step's own identity, as it was
when a step was recorded. An entry stores it as `keyedBy`: provenance, never read by a replay. When a step's key no longer
finds the entry, comparing it with the run's says what changed.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `CacheSchema` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `PolicyVersion` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Project` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Platform` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `EngineName` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `EngineSpiVersion` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `AppIdentity` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `AgentContextDigest` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/Trace.cs#L15)

## Properties

<a id="E2E_TraceKeyContext_AgentContextDigest_"></a>
<a id="E2E_TraceKeyContext_AgentContextDigest"></a>

### AgentContextDigest


```csharp
public string AgentContextDigest { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/Trace.cs#L23)

<a id="E2E_TraceKeyContext_AppIdentity_"></a>
<a id="E2E_TraceKeyContext_AppIdentity"></a>

### AppIdentity


```csharp
public string AppIdentity { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/Trace.cs#L22)

<a id="E2E_TraceKeyContext_CacheSchema_"></a>
<a id="E2E_TraceKeyContext_CacheSchema"></a>

### CacheSchema


```csharp
public string CacheSchema { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/Trace.cs#L16)

<a id="E2E_TraceKeyContext_EngineName_"></a>
<a id="E2E_TraceKeyContext_EngineName"></a>

### EngineName


```csharp
public string EngineName { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/Trace.cs#L20)

<a id="E2E_TraceKeyContext_EngineSpiVersion_"></a>
<a id="E2E_TraceKeyContext_EngineSpiVersion"></a>

### EngineSpiVersion


```csharp
public int EngineSpiVersion { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/Trace.cs#L21)

<a id="E2E_TraceKeyContext_Platform_"></a>
<a id="E2E_TraceKeyContext_Platform"></a>

### Platform


```csharp
public string Platform { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/Trace.cs#L19)

<a id="E2E_TraceKeyContext_PolicyVersion_"></a>
<a id="E2E_TraceKeyContext_PolicyVersion"></a>

### PolicyVersion


```csharp
public string PolicyVersion { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/Trace.cs#L17)

<a id="E2E_TraceKeyContext_Project_"></a>
<a id="E2E_TraceKeyContext_Project"></a>

### Project


```csharp
public string Project { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/Trace.cs#L18)
