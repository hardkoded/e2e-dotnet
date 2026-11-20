---
title: "ModelEntry"
description: "Upstream's ModelEntry: one provider-owned model record."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelEntry"
  hidden: true
---

<a id="E2E_Decision_Sdk_ModelEntry"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record ModelEntry : IEquatable<ModelEntry>
```


Upstream's `ModelEntry`: one provider-owned model record.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ModelEntry&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ModelCapabilities.cs)

## Constructors

<a id="E2E_Decision_Sdk_ModelEntry__ctor_"></a>
<a id="E2E_Decision_Sdk_ModelEntry__ctor_E2E_Decision_Sdk_ModelCapabilities_"></a>

### ModelEntry(ModelCapabilities)


```csharp
public ModelEntry(ModelCapabilities Capabilities)
```


Upstream's `ModelEntry`: one provider-owned model record.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Capabilities` | [ModelCapabilities](/api/E2E.Decision.Sdk/ModelCapabilities/) |  |

## Properties

<a id="E2E_Decision_Sdk_ModelEntry_Capabilities_"></a>
<a id="E2E_Decision_Sdk_ModelEntry_Capabilities"></a>

### Capabilities


```csharp
public ModelCapabilities Capabilities { get; init; }
```


**Value:** [ModelCapabilities](/api/E2E.Decision.Sdk/ModelCapabilities/)

<a id="E2E_Decision_Sdk_ModelEntry_ProviderOptions_"></a>
<a id="E2E_Decision_Sdk_ModelEntry_ProviderOptions"></a>

### ProviderOptions


```csharp
public ModelProviderOptions? ProviderOptions { get; init; }
```


Omitted sections or options are undeclared, not explicitly unsupported.

**Value:** [ModelProviderOptions](/api/E2E.Decision.Sdk/ModelProviderOptions/)
