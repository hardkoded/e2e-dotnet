---
title: "GatewayAuthenticationException"
description: "Authentication failed: an invalid API key or OIDC token."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayAuthenticationException"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayAuthenticationException"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayAuthenticationException : GatewayException, ISerializable
```


Authentication failed: an invalid API key or OIDC token.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [GatewayException](/api/E2E.Decision.Sdk.Gateway/GatewayException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayErrors.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayAuthenticationException__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAuthenticationException__ctor_System_String_System_Int32_System_Exception_System_String_"></a>

### GatewayAuthenticationException(string, int, Exception?, string?)


```csharp
public GatewayAuthenticationException(string message = "Authentication failed", int statusCode = 401, Exception? cause = null, string? generationId = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `statusCode` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `cause` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |
| `generationId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayAuthenticationException_Name_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAuthenticationException_Name"></a>

### Name


```csharp
public override string Name { get; }
```


The error's class name upstream, such as `GatewayInvalidRequestError`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayAuthenticationException_Type_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAuthenticationException_Type"></a>

### Type


```csharp
public override string Type { get; }
```


The error type the Gateway reports, such as `invalid_request_error`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Decision_Sdk_Gateway_GatewayAuthenticationException_CreateContextualError_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAuthenticationException_CreateContextualError_System_Boolean_System_Boolean_System_Int32_System_Exception_System_String_"></a>

### CreateContextualError(bool, bool, int, Exception?, string?)


```csharp
public static GatewayAuthenticationException CreateContextualError(bool apiKeyProvided, bool oidcTokenProvided, int statusCode = 401, Exception? cause = null, string? generationId = null)
```


Upstream's `createContextualError`: the message names how to authenticate.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `apiKeyProvided` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `oidcTokenProvided` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `statusCode` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `cause` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |
| `generationId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [GatewayAuthenticationException](./)
