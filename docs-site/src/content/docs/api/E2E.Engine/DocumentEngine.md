---
title: "DocumentEngine"
description: "In-memory page engine for samples and tests that do not need a browser. Each open builds a fresh page from the route mapped on DocumentWorld. Actions mutate..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DocumentEngine"
  hidden: true
---

<a id="E2E_Engine_DocumentEngine"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class DocumentEngine : IEngine
```


In-memory page engine for samples and tests that do not need a browser.
Each open builds a fresh page from the route mapped on
[DocumentWorld](/api/E2E.Engine/DocumentWorld/). Actions mutate that page until the next open.
`app.back` rebuilds the previous route, and `app.restart` and
`app.clearState` drop the history and reopen the base URL. The document engine
has no browser, so the [Browser](/api/E2E/Browser/) fixture is unsupported.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEngine](/api/E2E.Engine/IEngine/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L17)

## Constructors

<a id="E2E_Engine_DocumentEngine__ctor_"></a>
<a id="E2E_Engine_DocumentEngine__ctor_E2E_Engine_DocumentWorld_"></a>

### DocumentEngine(DocumentWorld)


```csharp
public DocumentEngine(DocumentWorld world)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `world` | [DocumentWorld](/api/E2E.Engine/DocumentWorld/) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L23)

## Fields

<a id="E2E_Engine_DocumentEngine_EngineVersion"></a>

### EngineVersion


```csharp
public const string EngineVersion = "1.0.0"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L19)

## Properties

<a id="E2E_Engine_DocumentEngine_Capabilities_"></a>
<a id="E2E_Engine_DocumentEngine_Capabilities"></a>

### Capabilities


```csharp
public EngineCapabilities Capabilities { get; }
```


**Value:** [EngineCapabilities](/api/E2E.Engine/EngineCapabilities/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L33)

<a id="E2E_Engine_DocumentEngine_Platform_"></a>
<a id="E2E_Engine_DocumentEngine_Platform"></a>

### Platform


```csharp
public string Platform { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L29)

<a id="E2E_Engine_DocumentEngine_Version_"></a>
<a id="E2E_Engine_DocumentEngine_Version"></a>

### Version


```csharp
public string Version { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L31)

## Methods

<a id="E2E_Engine_DocumentEngine_StartAsync_"></a>
<a id="E2E_Engine_DocumentEngine_StartAsync_E2E_Engine_EngineStartOptions_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/DocumentEngine.cs#L41)
