---
title: "OpenCodeConsoleModel"
description: "An OpenCode Console model. The first call reads the workspace config and talks to the model over its own protocol: chat completions, OpenAI Responses, Anthro..."
editUrl: false
prev: false
next: false
sidebar:
  label: "OpenCodeConsoleModel"
  hidden: true
---

<a id="E2E_OAuth_OpenCodeConsoleModel"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class OpenCodeConsoleModel : DelegatingModel, IAgentModel, IDisposable
```


An OpenCode Console model. The first call reads the workspace config and talks to the model over its own
protocol: chat completions, OpenAI Responses, Anthropic Messages, or Gemini. Each instance names one session,
so its calls stay on one upstream and its cache.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [DelegatingModel](/api/E2E.OAuth/DelegatingModel/)

**Implements:** [IAgentModel](/api/E2E/IAgentModel/), [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L139)

## Properties

<a id="E2E_OAuth_OpenCodeConsoleModel_Fallback_"></a>
<a id="E2E_OAuth_OpenCodeConsoleModel_Fallback"></a>

### Fallback


```csharp
protected override IAgentModel Fallback { get; }
```


The model to use until a lookup succeeds.

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L158)

## Methods

<a id="E2E_OAuth_OpenCodeConsoleModel_ResolveAsync_"></a>
<a id="E2E_OAuth_OpenCodeConsoleModel_ResolveAsync_System_Threading_CancellationToken_"></a>

### ResolveAsync(CancellationToken)


```csharp
protected override Task<IAgentModel?> ResolveAsync(CancellationToken cancellationToken)
```


The model for the looked-up protocol, or null when the lookup could not be read.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IAgentModel&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L160)
