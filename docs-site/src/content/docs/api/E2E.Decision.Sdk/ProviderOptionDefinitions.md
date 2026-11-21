---
title: "ProviderOptionDefinitions"
description: "Upstream's ProviderOptionDefinitions: the provider options a provider accepts, grouped by operation."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProviderOptionDefinitions"
  hidden: true
---

<a id="E2E_Decision_Sdk_ProviderOptionDefinitions"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record ProviderOptionDefinitions : IEquatable<ProviderOptionDefinitions>
```


Upstream's `ProviderOptionDefinitions`: the provider options a provider accepts, grouped by operation.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ProviderOptionDefinitions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ModelCapabilities.cs)

## Properties

<a id="E2E_Decision_Sdk_ProviderOptionDefinitions_Decision_"></a>
<a id="E2E_Decision_Sdk_ProviderOptionDefinitions_Decision"></a>

### Decision


```csharp
public IReadOnlyDictionary<string, ProviderOptionDefinition>? Decision { get; init; }
```


The decision section.

**Value:** [IReadOnlyDictionary&lt;string, ProviderOptionDefinition&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
