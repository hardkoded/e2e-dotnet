---
title: "GatewayException"
description: "The AI SDK's GatewayError (packages/gateway/src/errors): a failure of the Vercel AI Gateway."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayException"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayException"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public abstract class GatewayException : Exception, ISerializable
```


The AI SDK's `GatewayError` (`packages/gateway/src/errors`): a failure of the Vercel AI Gateway.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

**Derived:** [GatewayAccessPolicyDeniedException](/api/E2E.Decision.Sdk.Gateway/GatewayAccessPolicyDeniedException/), [GatewayAuthenticationException](/api/E2E.Decision.Sdk.Gateway/GatewayAuthenticationException/), [GatewayFailedDependencyException](/api/E2E.Decision.Sdk.Gateway/GatewayFailedDependencyException/), [GatewayForbiddenException](/api/E2E.Decision.Sdk.Gateway/GatewayForbiddenException/), [GatewayInternalServerException](/api/E2E.Decision.Sdk.Gateway/GatewayInternalServerException/), [GatewayInvalidRequestException](/api/E2E.Decision.Sdk.Gateway/GatewayInvalidRequestException/), [GatewayModelNotFoundException](/api/E2E.Decision.Sdk.Gateway/GatewayModelNotFoundException/), [GatewayNotFoundException](/api/E2E.Decision.Sdk.Gateway/GatewayNotFoundException/), [GatewayRateLimitException](/api/E2E.Decision.Sdk.Gateway/GatewayRateLimitException/), [GatewayResponseException](/api/E2E.Decision.Sdk.Gateway/GatewayResponseException/), [GatewayTimeoutException](/api/E2E.Decision.Sdk.Gateway/GatewayTimeoutException/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayErrors.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayException__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayException__ctor_System_String_System_Int32_System_Exception_System_String_System_Nullable_System_Boolean__"></a>

### GatewayException(string, int, Exception?, string?, bool?)


```csharp
protected GatewayException(string message, int statusCode, Exception? cause, string? generationId, bool? isRetryable = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `statusCode` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `cause` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |
| `generationId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `isRetryable` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayException_GenerationId_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayException_GenerationId"></a>

### GenerationId


```csharp
public string? GenerationId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayException_IsRetryable_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayException_IsRetryable"></a>

### IsRetryable


```csharp
public bool IsRetryable { get; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_Gateway_GatewayException_Name_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayException_Name"></a>

### Name


```csharp
public abstract string Name { get; }
```


The error's class name upstream, such as `GatewayInvalidRequestError`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayException_StatusCode_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayException_StatusCode"></a>

### StatusCode


```csharp
public int StatusCode { get; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Decision_Sdk_Gateway_GatewayException_Type_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayException_Type"></a>

### Type


```csharp
public abstract string Type { get; }
```


The error type the Gateway reports, such as `invalid_request_error`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
