---
title: "WebEngine"
description: "Browser engine for the web target. It drives Chromium, Firefox, or WebKit through Playwright, reads a semantic tree (role, name, text, test id, state), and p..."
editUrl: false
prev: false
next: false
sidebar:
  label: "WebEngine"
  hidden: true
---

<a id="E2E_Engine_WebEngine"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class WebEngine : IDoctorEngine, IEngine, IAsyncDisposable
```


Browser engine for the web target. It drives Chromium, Firefox, or WebKit through Playwright,
reads a semantic tree (role, name, text, test id, state), and performs the
same node actions the document engine does. Install browsers once with
`playwright.ps1 install chromium` (or `firefox`, `webkit`) from this project's build output.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IDoctorEngine](/api/E2E.Engine/IDoctorEngine/), [IEngine](/api/E2E.Engine/IEngine/), [IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Constructors

<a id="E2E_Engine_WebEngine__ctor_"></a>
<a id="E2E_Engine_WebEngine__ctor_E2E_Engine_WebEngineOptions_"></a>

### WebEngine(WebEngineOptions)


```csharp
public WebEngine(WebEngineOptions options)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [WebEngineOptions](/api/E2E.Engine/WebEngineOptions/) |  |

<a id="E2E_Engine_WebEngine__ctor_System_Nullable_System_Boolean__"></a>

### WebEngine(bool?)


```csharp
public WebEngine(bool? headless = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `headless` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

## Fields

<a id="E2E_Engine_WebEngine_EngineVersion"></a>

### EngineVersion


```csharp
public const string EngineVersion = "1.1.0"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Properties

<a id="E2E_Engine_WebEngine_Capabilities_"></a>
<a id="E2E_Engine_WebEngine_Capabilities"></a>

### Capabilities


```csharp
public EngineCapabilities Capabilities { get; }
```


**Value:** [EngineCapabilities](/api/E2E.Engine/EngineCapabilities/)

<a id="E2E_Engine_WebEngine_Platform_"></a>
<a id="E2E_Engine_WebEngine_Platform"></a>

### Platform


```csharp
public string Platform { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_WebEngine_Secrets_"></a>
<a id="E2E_Engine_WebEngine_Secrets"></a>

### Secrets


```csharp
public IReadOnlyList<Secret> Secrets { get; }
```


The secret the options hold: the [WebBasicAuth](/api/E2E.Engine/WebBasicAuth/) password when it is a [Secret](/api/E2E/Secret/). Upstream's `web()` declares it as `secrets`.

**Value:** [IReadOnlyList&lt;Secret&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_WebEngine_Version_"></a>
<a id="E2E_Engine_WebEngine_Version"></a>

### Version


```csharp
public string Version { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Engine_WebEngine_DisposeAsync_"></a>
<a id="E2E_Engine_WebEngine_DisposeAsync"></a>

### DisposeAsync()


```csharp
public ValueTask DisposeAsync()
```


Worker shutdown: releases the replacement lease the worker acquired for itself, within the cleanup timeout. What prepare leased is the runner's to release.

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

<a id="E2E_Engine_WebEngine_DoctorAsync_"></a>
<a id="E2E_Engine_WebEngine_DoctorAsync_E2E_Engine_EngineDoctorInfo_System_Threading_CancellationToken_"></a>

### DoctorAsync(EngineDoctorInfo, CancellationToken)


```csharp
public Task<IReadOnlyList<EngineDoctorCheck>> DoctorAsync(EngineDoctorInfo info, CancellationToken cancellationToken)
```


The checks of `e2e doctor`: the init scripts a start reads, and the browser, which only a local launch needs on this machine.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `info` | [EngineDoctorInfo](/api/E2E.Engine/EngineDoctorInfo/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IReadOnlyList&lt;EngineDoctorCheck&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_WebEngine_FinishAsync_"></a>
<a id="E2E_Engine_WebEngine_FinishAsync_E2E_Engine_EngineFinishInfo_System_Threading_CancellationToken_"></a>

### FinishAsync(EngineFinishInfo, CancellationToken)


```csharp
public Task FinishAsync(EngineFinishInfo info, CancellationToken cancellationToken)
```


Releases the browsers `PrepareAsync` leased, once per run and target, then has the provider sweep up the
leases a worker that died left open. Call it after every worker ended, on every exit path, with a cleanup budget in
&lt;code class="paramref"&gt;cancellationToken&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `info` | [EngineFinishInfo](/api/E2E.Engine/EngineFinishInfo/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

<a id="E2E_Engine_WebEngine_Init_"></a>
<a id="E2E_Engine_WebEngine_Init_E2E_Engine_EngineInitInfo_"></a>

### Init(EngineInitInfo)


```csharp
public void Init(EngineInitInfo info)
```


Starts the worker of one slot: records the run it serves and, in [Worker](/api/E2E.Engine/BrowserProviderScope/#E2E_Engine_BrowserProviderScope_Worker) scope, the
lease its slot was handed. Every `StartAsync` after it rides that lease, or a replacement once its browser
dropped. It can be called again on a disposed engine.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `info` | [EngineInitInfo](/api/E2E.Engine/EngineInitInfo/) |  |

<a id="E2E_Engine_WebEngine_PrepareAsync_"></a>
<a id="E2E_Engine_WebEngine_PrepareAsync_E2E_Engine_EnginePrepareInfo_System_Threading_CancellationToken_"></a>

### PrepareAsync(EnginePrepareInfo, CancellationToken)


```csharp
public Task<EnginePrepareResult> PrepareAsync(EnginePrepareInfo info, CancellationToken cancellationToken)
```


Leases one browser per worker slot the run will use from the provider, once per run and target, before any worker
starts. The result's environment is what the workers read in [Init(EngineInitInfo)](#E2E_Engine_WebEngine_Init_E2E_Engine_EngineInitInfo_). An engine with no provider has
nothing to prepare.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `info` | [EnginePrepareInfo](/api/E2E.Engine/EnginePrepareInfo/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;EnginePrepareResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_Engine_WebEngine_StartAsync_"></a>
<a id="E2E_Engine_WebEngine_StartAsync_E2E_Engine_EngineStartOptions_System_Threading_CancellationToken_"></a>

### StartAsync(EngineStartOptions, CancellationToken)


```csharp
public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [EngineStartOptions](/api/E2E.Engine/EngineStartOptions/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IEngineSession&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
