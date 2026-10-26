---
title: "CopilotModel"
description: "A GitHub Copilot subscription model: chat completions, or the Responses API when the plan serves the model only there."
editUrl: false
prev: false
next: false
sidebar:
  label: "CopilotModel"
  hidden: true
---

<a id="E2E_OAuth_CopilotModel"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class CopilotModel : DelegatingModel, IAgentModel, IDisposable
```


A GitHub Copilot subscription model: chat completions, or the Responses API when the plan serves the model only there.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [DelegatingModel](/api/E2E.OAuth/DelegatingModel/)

**Implements:** [IAgentModel](/api/E2E/IAgentModel/), [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L102)

## Constructors

<a id="E2E_OAuth_CopilotModel__ctor_"></a>
<a id="E2E_OAuth_CopilotModel__ctor_System_String_System_Net_Http_HttpClient_"></a>

### CopilotModel(string, HttpClient)


```csharp
public CopilotModel(string model, HttpClient http)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `http` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L107)

## Properties

<a id="E2E_OAuth_CopilotModel_Fallback_"></a>
<a id="E2E_OAuth_CopilotModel_Fallback"></a>

### Fallback


```csharp
protected override IAgentModel Fallback { get; }
```


The model to use until a lookup succeeds.

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L117)

## Methods

<a id="E2E_OAuth_CopilotModel_Refusal_"></a>
<a id="E2E_OAuth_CopilotModel_Refusal_E2E_AgentException_"></a>

### Refusal(AgentException)


```csharp
protected override string? Refusal(AgentException failure)
```


Why the account cannot use the model, when &lt;code class="paramref"&gt;failure&lt;/code&gt; is the provider refusing it; null for any other failure.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `failure` | [AgentException](/api/E2E/AgentException/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L119)

<a id="E2E_OAuth_CopilotModel_ResolveAsync_"></a>
<a id="E2E_OAuth_CopilotModel_ResolveAsync_System_Threading_CancellationToken_"></a>

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L121)
