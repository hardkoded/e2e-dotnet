---
title: "GatewayErrors"
description: "The AI SDK's createGatewayErrorFromResponse and asGatewayError."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayErrors"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayErrors"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public static class GatewayErrors
```


The AI SDK's `createGatewayErrorFromResponse` and `asGatewayError`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayErrors.cs)

## Methods

<a id="E2E_Decision_Sdk_Gateway_GatewayErrors_AsGatewayError_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayErrors_AsGatewayError_System_Exception_System_String_"></a>

### AsGatewayError(Exception, string?)


```csharp
public static GatewayException AsGatewayError(Exception error, string? authMethod = null)
```


Upstream's `asGatewayError`: a Gateway error stays; a client timeout becomes a timeout error; a failed call becomes the error
its body names; anything else is a 500. A caller's cancellation is not a Gateway failure and is not wrapped.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `error` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |
| `authMethod` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [GatewayException](/api/E2E.Decision.Sdk.Gateway/GatewayException/)

<a id="E2E_Decision_Sdk_Gateway_GatewayErrors_CreateGatewayErrorFromResponse_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayErrors_CreateGatewayErrorFromResponse_System_Nullable_System_Text_Json_JsonElement__System_Int32_System_String_System_Exception_System_String_System_Nullable_System_Boolean__"></a>

### CreateGatewayErrorFromResponse(JsonElement?, int, string, Exception?, string?, bool?)


```csharp
public static GatewayException CreateGatewayErrorFromResponse(JsonElement? response, int statusCode, string defaultMessage = "Gateway request failed", Exception? cause = null, string? authMethod = null, bool? isRetryable = null)
```


Upstream's `createGatewayErrorFromResponse`: the Gateway's error type picks the error.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `response` | [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) |  |
| `statusCode` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `defaultMessage` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cause` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |
| `authMethod` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `isRetryable` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [GatewayException](/api/E2E.Decision.Sdk.Gateway/GatewayException/)
