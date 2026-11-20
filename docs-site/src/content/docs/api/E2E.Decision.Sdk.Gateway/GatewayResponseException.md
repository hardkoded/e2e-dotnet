---
title: "GatewayResponseException"
description: "Gateway response parsing error: the error body is not the shape the Gateway documents."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayResponseException"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayResponseException"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayResponseException : GatewayException, ISerializable
```


Gateway response parsing error: the error body is not the shape the Gateway documents.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [GatewayException](/api/E2E.Decision.Sdk.Gateway/GatewayException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayErrors.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayResponseException__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayResponseException__ctor_System_String_System_Int32_System_String_System_Exception_System_String_System_Nullable_System_Boolean__System_String_"></a>

### GatewayResponseException(string, int, string?, Exception?, string?, bool?, string?)


```csharp
public GatewayResponseException(string message = "Invalid response from Gateway", int statusCode = 502, string? response = null, Exception? cause = null, string? generationId = null, bool? isRetryable = null, string? validationError = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `statusCode` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `response` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cause` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |
| `generationId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `isRetryable` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `validationError` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayResponseException_Name_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayResponseException_Name"></a>

### Name


```csharp
public override string Name { get; }
```


The error's class name upstream, such as `GatewayInvalidRequestError`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayResponseException_Response_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayResponseException_Response"></a>

### Response


```csharp
public string? Response { get; }
```


The body that did not parse, as text: a JSON string is its text, JSON null is null, anything else is its JSON.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayResponseException_Type_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayResponseException_Type"></a>

### Type


```csharp
public override string Type { get; }
```


The error type the Gateway reports, such as `invalid_request_error`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayResponseException_ValidationError_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayResponseException_ValidationError"></a>

### ValidationError


```csharp
public string? ValidationError { get; }
```


Why the body is not the shape the Gateway documents (upstream keeps the Zod error).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
