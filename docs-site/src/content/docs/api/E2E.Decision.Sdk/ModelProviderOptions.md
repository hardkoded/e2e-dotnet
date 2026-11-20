---
title: "ModelProviderOptions"
description: "Upstream's ModelProviderOptions: per-model support for a provider's options, keyed by section and option name."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelProviderOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_ModelProviderOptions"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record ModelProviderOptions : IEquatable<ModelProviderOptions>
```


Upstream's `ModelProviderOptions`: per-model support for a provider's options, keyed by section and option name.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ModelProviderOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ModelCapabilities.cs)

## Properties

<a id="E2E_Decision_Sdk_ModelProviderOptions_Decision_"></a>
<a id="E2E_Decision_Sdk_ModelProviderOptions_Decision"></a>

### Decision


```csharp
public IReadOnlyDictionary<string, ModelCapability>? Decision { get; init; }
```


The decision section.

**Value:** [IReadOnlyDictionary&lt;string, ModelCapability&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)
