---
title: "IDisposableEngine"
description: "An engine with upstream's optional dispose hook (declared in engine/index.ts): worker shutdown within the cleanup budget, where the engine releases what its..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IDisposableEngine"
  hidden: true
---

<a id="E2E_Engine_IDisposableEngine"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IDisposableEngine : IEngine
```


An engine with upstream's optional `dispose` hook (declared in `engine/index.ts`): worker shutdown within the cleanup budget,
where the engine releases what its workers hold, or keeps the run-owned part of it when [ReuseSignal](/api/E2E.Engine/EngineDisposeContext/#E2E_Engine_EngineDisposeContext_ReuseSignal) is live.
.NET-only name: upstream's engine has the member in the same contract as `prepare` and `finish`.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IPreparableEngine.cs)

## Methods

<a id="E2E_Engine_IDisposableEngine_DisposeAsync_"></a>
<a id="E2E_Engine_IDisposableEngine_DisposeAsync_E2E_Engine_EngineDisposeContext_System_Threading_CancellationToken_"></a>

### DisposeAsync(EngineDisposeContext, CancellationToken)


```csharp
Task DisposeAsync(EngineDisposeContext context, CancellationToken cancellationToken)
```


Shuts down the engine's workers, within [CleanupTimeout](/api/E2E.Engine/EngineDisposeContext/#E2E_Engine_EngineDisposeContext_CleanupTimeout). A failure is a cleanup error.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | [EngineDisposeContext](/api/E2E.Engine/EngineDisposeContext/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
