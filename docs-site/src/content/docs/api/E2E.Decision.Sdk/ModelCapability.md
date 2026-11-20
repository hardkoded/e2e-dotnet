---
title: "ModelCapability"
description: "The AI SDK's ModelCapability: true, false, 'unknown', or { condition }. A condition is documentation, not an expression to execute."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelCapability"
  hidden: true
---

<a id="E2E_Decision_Sdk_ModelCapability"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record ModelCapability : IEquatable<ModelCapability>
```


The AI SDK's `ModelCapability`: `true`, `false`, `'unknown'`, or `{ condition }`. A condition is documentation,
not an expression to execute.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ModelCapability&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/ModelCapabilities.cs)

## Properties

<a id="E2E_Decision_Sdk_ModelCapability_Condition_"></a>
<a id="E2E_Decision_Sdk_ModelCapability_Condition"></a>

### Condition


```csharp
public string? Condition { get; init; }
```


Upstream's `{ condition }`: when support is available.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_ModelCapability_Supported_"></a>
<a id="E2E_Decision_Sdk_ModelCapability_Supported"></a>

### Supported


```csharp
public static ModelCapability Supported { get; }
```


Supported.

**Value:** [ModelCapability](./)

<a id="E2E_Decision_Sdk_ModelCapability_Unknown_"></a>
<a id="E2E_Decision_Sdk_ModelCapability_Unknown"></a>

### Unknown


```csharp
public static ModelCapability Unknown { get; }
```


Upstream's `'unknown'`: distinct from unsupported.

**Value:** [ModelCapability](./)

<a id="E2E_Decision_Sdk_ModelCapability_Unsupported_"></a>
<a id="E2E_Decision_Sdk_ModelCapability_Unsupported"></a>

### Unsupported


```csharp
public static ModelCapability Unsupported { get; }
```


Unsupported.

**Value:** [ModelCapability](./)

<a id="E2E_Decision_Sdk_ModelCapability_Value_"></a>
<a id="E2E_Decision_Sdk_ModelCapability_Value"></a>

### Value


```csharp
public bool? Value { get; init; }
```


True or false for a plain boolean capability; null for `'unknown'` and for a condition.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

## Methods

<a id="E2E_Decision_Sdk_ModelCapability_From_"></a>
<a id="E2E_Decision_Sdk_ModelCapability_From_System_Boolean_"></a>

### From(bool)


```csharp
public static ModelCapability From(bool value)
```


A boolean capability.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `value` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [ModelCapability](./)

<a id="E2E_Decision_Sdk_ModelCapability_WithCondition_"></a>
<a id="E2E_Decision_Sdk_ModelCapability_WithCondition_System_String_"></a>

### WithCondition(string)


```csharp
public static ModelCapability WithCondition(string condition)
```


Upstream's `{ condition }`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `condition` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ModelCapability](./)
