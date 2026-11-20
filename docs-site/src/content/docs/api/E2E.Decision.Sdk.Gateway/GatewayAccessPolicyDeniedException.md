---
title: "GatewayAccessPolicyDeniedException"
description: "Access policy denied: an AI Gateway access policy does not allow the requested model or provider."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayAccessPolicyDeniedException"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed class GatewayAccessPolicyDeniedException : GatewayException, ISerializable
```


Access policy denied: an AI Gateway access policy does not allow the requested model or provider.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Exception](https://learn.microsoft.com/dotnet/api/system.exception) → [GatewayException](/api/E2E.Decision.Sdk.Gateway/GatewayException/)

**Implements:** [ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayErrors.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException__ctor_System_String_System_Int32_System_Exception_System_String_System_Collections_Generic_IReadOnlyList_System_String__System_Collections_Generic_IReadOnlyList_System_String__System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `GatewayAccessPolicyDeniedException(string, int, Exception?, string?, IReadOnlyList<string>?, IReadOnlyList<string>?, IReadOnlyList<string>?)`


```csharp
public GatewayAccessPolicyDeniedException(string message = "Access policy denied", int statusCode = 403, Exception? cause = null, string? generationId = null, IReadOnlyList<string>? policyIds = null, IReadOnlyList<string>? removedModels = null, IReadOnlyList<string>? removedProviders = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `statusCode` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |
| `cause` | [Exception](https://learn.microsoft.com/dotnet/api/system.exception) |  |
| `generationId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `policyIds` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `removedModels` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `removedProviders` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException_Name_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException_Name"></a>

### Name


```csharp
public override string Name { get; }
```


The error's class name upstream, such as `GatewayInvalidRequestError`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException_PolicyIds_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException_PolicyIds"></a>

### PolicyIds


```csharp
public IReadOnlyList<string>? PolicyIds { get; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException_RemovedModels_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException_RemovedModels"></a>

### RemovedModels


```csharp
public IReadOnlyList<string>? RemovedModels { get; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException_RemovedProviders_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException_RemovedProviders"></a>

### RemovedProviders


```csharp
public IReadOnlyList<string>? RemovedProviders { get; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException_Type_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayAccessPolicyDeniedException_Type"></a>

### Type


```csharp
public override string Type { get; }
```


The error type the Gateway reports, such as `invalid_request_error`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
