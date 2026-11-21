---
title: "ModelCapabilities"
description: "Upstream's ModelCapabilities. Only the decision section exists in the port."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelCapabilities"
  hidden: true
---

<a id="E2E_Decision_Sdk_ModelCapabilities"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record ModelCapabilities : IEquatable<ModelCapabilities>
```


Upstream's `ModelCapabilities`. Only the decision section exists in the port.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ModelCapabilities&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ModelCapabilities.cs)

## Properties

<a id="E2E_Decision_Sdk_ModelCapabilities_Decision_"></a>
<a id="E2E_Decision_Sdk_ModelCapabilities_Decision"></a>

### Decision


```csharp
public DecisionCapabilities? Decision { get; init; }
```


Omitted means undeclared, not explicitly unsupported.

**Value:** [DecisionCapabilities](/api/E2E.Decision.Sdk/DecisionCapabilities/)
