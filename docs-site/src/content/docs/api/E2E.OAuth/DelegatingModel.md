---
title: "DelegatingModel"
description: "A model that settles its protocol on the first call and delegates to the client for it. One lookup serves every concurrent call, under its own timeout, so a..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DelegatingModel"
  hidden: true
---

<a id="E2E_OAuth_DelegatingModel"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public abstract class DelegatingModel : IAgentModel, IDisposable
```


A model that settles its protocol on the first call and delegates to the client for it. One lookup serves every
concurrent call, under its own timeout, so a caller that gives up never fails the others. Only a lookup that
succeeded is remembered: when it could not be read, that call goes over the fallback and the next call asks again.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IAgentModel](/api/E2E/IAgentModel/), [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

**Derived:** [CopilotModel](/api/E2E.OAuth/CopilotModel/), [OpenCodeConsoleModel](/api/E2E.OAuth/OpenCodeConsoleModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L14)

## Constructors

<a id="E2E_OAuth_DelegatingModel__ctor_"></a>
<a id="E2E_OAuth_DelegatingModel__ctor_System_String_System_Net_Http_HttpClient_"></a>

### DelegatingModel(string, HttpClient)


```csharp
protected DelegatingModel(string name, HttpClient http)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `http` | [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L23)

## Properties

<a id="E2E_OAuth_DelegatingModel_Fallback_"></a>
<a id="E2E_OAuth_DelegatingModel_Fallback"></a>

### Fallback


```csharp
protected abstract IAgentModel Fallback { get; }
```


The model to use until a lookup succeeds.

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L95)

<a id="E2E_OAuth_DelegatingModel_Http_"></a>
<a id="E2E_OAuth_DelegatingModel_Http"></a>

### Http


```csharp
protected HttpClient Http { get; }
```


**Value:** [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L31)

<a id="E2E_OAuth_DelegatingModel_Name_"></a>
<a id="E2E_OAuth_DelegatingModel_Name"></a>

### Name


```csharp
public string Name { get; }
```


Provider and model id, printed in the run summary. Changing it does not miss the replay cache.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L29)

## Methods

<a id="E2E_OAuth_DelegatingModel_CompleteAsync_"></a>
<a id="E2E_OAuth_DelegatingModel_CompleteAsync_E2E_ModelRequest_System_Threading_CancellationToken_"></a>

### CompleteAsync(ModelRequest, CancellationToken)


```csharp
public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [ModelRequest](/api/E2E/ModelRequest/) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;ModelResponse&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L33)

<a id="E2E_OAuth_DelegatingModel_Dispose_"></a>
<a id="E2E_OAuth_DelegatingModel_Dispose"></a>

### Dispose()


```csharp
public void Dispose()
```


Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L58)

<a id="E2E_OAuth_DelegatingModel_Refusal_"></a>
<a id="E2E_OAuth_DelegatingModel_Refusal_E2E_AgentException_"></a>

### Refusal(AgentException)


```csharp
protected virtual string? Refusal(AgentException failure)
```


Why the account cannot use the model, when &lt;code class="paramref"&gt;failure&lt;/code&gt; is the provider refusing it; null for any other failure.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `failure` | [AgentException](/api/E2E/AgentException/) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L92)

<a id="E2E_OAuth_DelegatingModel_ResolveAsync_"></a>
<a id="E2E_OAuth_DelegatingModel_ResolveAsync_System_Threading_CancellationToken_"></a>

### ResolveAsync(CancellationToken)


```csharp
protected abstract Task<IAgentModel?> ResolveAsync(CancellationToken cancellationToken)
```


The model for the looked-up protocol, or null when the lookup could not be read.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;IAgentModel&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/SubscriptionModels.cs#L98)
