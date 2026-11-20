---
title: "ProviderRegistryOptions"
description: "The decision members of the options of the AI SDK's createProviderRegistry (ai/src/registry/provider-registry.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProviderRegistryOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_ProviderRegistryOptions"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class ProviderRegistryOptions
```


The decision members of the options of the AI SDK's `createProviderRegistry` (`ai/src/registry/provider-registry.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ProviderRegistry.cs)

## Properties

<a id="E2E_Decision_Sdk_ProviderRegistryOptions_Separator_"></a>
<a id="E2E_Decision_Sdk_ProviderRegistryOptions_Separator"></a>

### Separator


```csharp
public string Separator { get; init; }
```


The separator between the provider id and the model id of a combined id. Defaults to `:`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
