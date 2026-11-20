---
title: "GatewayCreditsResponse"
description: "Upstream's GatewayCreditsResponse."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayCreditsResponse"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayCreditsResponse"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed record GatewayCreditsResponse : IEquatable<GatewayCreditsResponse>
```


Upstream's `GatewayCreditsResponse`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;GatewayCreditsResponse&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayFetchMetadata.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayCreditsResponse__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayCreditsResponse__ctor_System_String_System_String_"></a>

### GatewayCreditsResponse(string, string)


```csharp
public GatewayCreditsResponse(string Balance, string TotalUsed)
```


Upstream's `GatewayCreditsResponse`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Balance` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The remaining gateway credit balance available for API usage. |
| `TotalUsed` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The total amount of gateway credits that have been consumed. |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayCreditsResponse_Balance_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayCreditsResponse_Balance"></a>

### Balance


```csharp
public string Balance { get; init; }
```


The remaining gateway credit balance available for API usage.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_Gateway_GatewayCreditsResponse_TotalUsed_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayCreditsResponse_TotalUsed"></a>

### TotalUsed


```csharp
public string TotalUsed { get; init; }
```


The total amount of gateway credits that have been consumed.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
