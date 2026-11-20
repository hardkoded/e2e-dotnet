---
title: "GatewayInternalServerException"
description: "Internal server error from the Gateway."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayInternalServerException"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayInternalServerException"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayInternalServerException : GatewayException, ISerializable
```


Internal server error from the Gateway.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [GatewayException](/api/E2E.Decision.Sdk.Gateway/GatewayException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayErrors.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayInternalServerException__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayInternalServerException__ctor_System_String_System_Int32_System_Exception_System_String_"></a>

### GatewayInternalServerException(string, int, Exception?, string?)


```csharp
public GatewayInternalServerException(string message = "Internal server error", int statusCode = 500, Exception? cause = null, string? generationId = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `statusCode` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `cause` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |
| `generationId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayInternalServerException_Name_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayInternalServerException_Name"></a>

### Name


```csharp
public override string Name { get; }
```


The error's class name upstream, such as `GatewayInvalidRequestError`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayInternalServerException_Type_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayInternalServerException_Type"></a>

### Type


```csharp
public override string Type { get; }
```


The error type the Gateway reports, such as `invalid_request_error`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
