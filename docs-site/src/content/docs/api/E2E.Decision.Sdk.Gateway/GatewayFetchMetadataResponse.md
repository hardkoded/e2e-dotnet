---
title: "GatewayFetchMetadataResponse"
description: "Upstream's GatewayFetchMetadataResponse."
editUrl: false
prev: false
next: false
sidebar:
  label: "GatewayFetchMetadataResponse"
  hidden: true
---

<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadataResponse"></a>

Namespace: [E2E.Decision.Sdk.Gateway](/api/E2E.Decision.Sdk.Gateway/)


```csharp
public sealed record GatewayFetchMetadataResponse : IEquatable<GatewayFetchMetadataResponse>
```


Upstream's `GatewayFetchMetadataResponse`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;GatewayFetchMetadataResponse&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/Gateway/GatewayFetchMetadata.cs)

## Constructors

<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadataResponse__ctor_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadataResponse__ctor_System_Collections_Generic_IReadOnlyList_E2E_Decision_Sdk_Gateway_GatewayLanguageModelEntry__"></a>

### `GatewayFetchMetadataResponse(IReadOnlyList<GatewayLanguageModelEntry>)`


```csharp
public GatewayFetchMetadataResponse(IReadOnlyList<GatewayLanguageModelEntry> Models)
```


Upstream's `GatewayFetchMetadataResponse`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Models` | [IReadOnlyList&lt;GatewayLanguageModelEntry&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadataResponse_Models_"></a>
<a id="E2E_Decision_Sdk_Gateway_GatewayFetchMetadataResponse_Models"></a>

### Models


```csharp
public IReadOnlyList<GatewayLanguageModelEntry> Models { get; init; }
```


**Value:** [IReadOnlyList&lt;GatewayLanguageModelEntry&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
