---
title: "FetchFunction"
description: "The AI SDK's FetchFunction (@ai-sdk/provider-utils): the transport a download goes through. It must not follow redirects itself, because every hop is validat..."
editUrl: false
prev: false
next: false
sidebar:
  label: "FetchFunction"
  hidden: true
---

<a id="E2E_Decision_Sdk_FetchFunction"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public delegate Task<HttpResponseMessage> FetchFunction(HttpRequestMessage request, CancellationToken cancellationToken)
```


The AI SDK's `FetchFunction` (`@ai-sdk/provider-utils`): the transport a download goes through. It must not follow redirects
itself, because every hop is validated before it is requested.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [HttpRequestMessage](https://learn.microsoft.com/dotnet/api/system.net.http.httprequestmessage) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;HttpResponseMessage&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/FetchFunction.cs)
