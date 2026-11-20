---
title: "ProviderRegistry"
description: "The AI SDK's createProviderRegistry and DefaultProviderRegistry (provider-registry.ts), for decision models."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProviderRegistry"
  hidden: true
---

<a id="E2E_Decision_Sdk_ProviderRegistry"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public static class ProviderRegistry
```


The AI SDK's `createProviderRegistry` and `DefaultProviderRegistry` (`provider-registry.ts`), for decision models.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ProviderRegistry.cs)

## Methods

<a id="E2E_Decision_Sdk_ProviderRegistry_CreateProviderRegistry_"></a>
<a id="E2E_Decision_Sdk_ProviderRegistry_CreateProviderRegistry_System_Collections_Generic_IReadOnlyDictionary_System_String_E2E_Decision_Sdk_IDecisionProvider__E2E_Decision_Sdk_ProviderRegistryOptions_"></a>

### `CreateProviderRegistry(IReadOnlyDictionary<string, IDecisionProvider>, ProviderRegistryOptions?)`


```csharp
public static IProviderRegistryProvider CreateProviderRegistry(IReadOnlyDictionary<string, IDecisionProvider> providers, ProviderRegistryOptions? options = null)
```


Upstream's `createProviderRegistry`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `providers` | [IReadOnlyDictionary&lt;string, IDecisionProvider&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2) |  |
| `options` | [ProviderRegistryOptions](/api/E2E.Decision.Sdk/ProviderRegistryOptions/) |  |

**Returns:** [IProviderRegistryProvider](/api/E2E.Decision.Sdk/IProviderRegistryProvider/)
