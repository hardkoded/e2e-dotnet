---
title: "ProviderOptionDefinition"
description: "A provider option, declared once at the provider level. Upstream's ProviderOptionDefinition."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProviderOptionDefinition"
  hidden: true
---

<a id="E2E_Decision_Sdk_ProviderOptionDefinition"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record ProviderOptionDefinition : IEquatable<ProviderOptionDefinition>
```


A provider option, declared once at the provider level. Upstream's `ProviderOptionDefinition`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ProviderOptionDefinition&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ModelCapabilities.cs)

## Constructors

<a id="E2E_Decision_Sdk_ProviderOptionDefinition__ctor_"></a>
<a id="E2E_Decision_Sdk_ProviderOptionDefinition__ctor_System_String_"></a>

### ProviderOptionDefinition(string)


```csharp
public ProviderOptionDefinition(string Description)
```


A provider option, declared once at the provider level. Upstream's `ProviderOptionDefinition`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Description` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_ProviderOptionDefinition_Description_"></a>
<a id="E2E_Decision_Sdk_ProviderOptionDefinition_Description"></a>

### Description


```csharp
public string Description { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
