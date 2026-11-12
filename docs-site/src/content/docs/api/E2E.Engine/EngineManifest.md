---
title: "EngineManifest"
description: "Upstream's defineEngine and isEngineHandle (engine/manifest.ts). The engine manifest: DefineEngine(EngineSpec) validates an engine at config load, synchronou..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineManifest"
  hidden: true
---

<a id="E2E_Engine_EngineManifest"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public static class EngineManifest
```


Upstream's `defineEngine` and `isEngineHandle` (`engine/manifest.ts`). The engine manifest: [DefineEngine(EngineSpec)](#E2E_Engine_EngineManifest_DefineEngine_E2E_Engine_EngineSpec_) validates an
engine at config load, synchronously and loudly, and computes its capability set. A combination the contract does not allow is
`INVALID_CONFIG`, never a silent demotion to a lower tier. .NET-only holder name: C# has no free functions.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Fields

<a id="E2E_Engine_EngineManifest_SpiVersion"></a>

### SpiVersion


```csharp
public const int SpiVersion = 1
```


The engine contract version this runner supports (upstream's `ENGINE_SPI_VERSION`).

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

<a id="E2E_Engine_EngineManifest_DefineEngine_"></a>
<a id="E2E_Engine_EngineManifest_DefineEngine_E2E_Engine_EngineSpec_"></a>

### DefineEngine(EngineSpec)


```csharp
public static EngineHandle DefineEngine(EngineSpec spec)
```


Validates &lt;code class="paramref"&gt;spec&lt;/code&gt; and computes the engine's capability set. Throws `INVALID_CONFIG` for a declared combination the
contract does not allow, naming the engine.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `spec` | [EngineSpec](/api/E2E.Engine/EngineSpec/) |  |

**Returns:** [EngineHandle](/api/E2E.Engine/EngineHandle/)

<a id="E2E_Engine_EngineManifest_IsEngineHandle_"></a>
<a id="E2E_Engine_EngineManifest_IsEngineHandle_System_Object_"></a>

### IsEngineHandle(object?)


```csharp
public static bool IsEngineHandle(object? value)
```


True when &lt;code class="paramref"&gt;value&lt;/code&gt; is an engine handle built by [DefineEngine(EngineSpec)](#E2E_Engine_EngineManifest_DefineEngine_E2E_Engine_EngineSpec_).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [object](https://learn.microsoft.com/dotnet/api/system.object) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)
