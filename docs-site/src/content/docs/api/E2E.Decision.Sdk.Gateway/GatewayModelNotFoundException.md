---
title: "GatewayModelNotFoundException"
description: "Model not found or not available."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayModelNotFoundException"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayModelNotFoundException"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayModelNotFoundException : GatewayException, ISerializable
```


Model not found or not available.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [GatewayException](/api/E2E.Decision.Sdk.Gateway/GatewayException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayErrors.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayModelNotFoundException__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayModelNotFoundException__ctor_System_String_System_Int32_System_String_System_Exception_System_String_"></a>

### GatewayModelNotFoundException(string, int, string?, Exception?, string?)


```csharp
public GatewayModelNotFoundException(string message = "Model not found", int statusCode = 404, string? modelId = null, Exception? cause = null, string? generationId = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `statusCode` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `modelId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cause` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |
| `generationId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayModelNotFoundException_ModelId_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayModelNotFoundException_ModelId"></a>

### ModelId


```csharp
public string? ModelId { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayModelNotFoundException_Name_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayModelNotFoundException_Name"></a>

### Name


```csharp
public override string Name { get; }
```


The error's class name upstream, such as `GatewayInvalidRequestError`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayModelNotFoundException_Type_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayModelNotFoundException_Type"></a>

### Type


```csharp
public override string Type { get; }
```


The error type the Gateway reports, such as `invalid_request_error`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
