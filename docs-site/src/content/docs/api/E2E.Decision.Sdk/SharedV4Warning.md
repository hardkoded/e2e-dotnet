---
title: "SharedV4Warning"
description: "The AI SDK's SharedV4Warning (shared/v4/shared-v4-warning.ts): a warning from the model, for example that a feature is unsupported or that a compatibility mo..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SharedV4Warning"
  hidden: true
---

<a id="E2E_Decision_Sdk_SharedV4Warning"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record SharedV4Warning : IEquatable<SharedV4Warning>
```


The AI SDK's `SharedV4Warning` (`shared/v4/shared-v4-warning.ts`): a warning from the model, for example that a feature is
unsupported or that a compatibility mode is used. [Type](#E2E_Decision_Sdk_SharedV4Warning_Type) is `unsupported`, `compatibility`, `deprecated`, or
`other`; the fields that type does not have are null.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;SharedV4Warning&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/SharedV4.cs)

## Properties

<a id="E2E_Decision_Sdk_SharedV4Warning_Details_"></a>
<a id="E2E_Decision_Sdk_SharedV4Warning_Details"></a>

### Details


```csharp
public string? Details { get; init; }
```


Additional details about the warning (`unsupported` and `compatibility`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_SharedV4Warning_Feature_"></a>
<a id="E2E_Decision_Sdk_SharedV4Warning_Feature"></a>

### Feature


```csharp
public string? Feature { get; init; }
```


The feature that is not supported or used in a compatibility mode (`unsupported` and `compatibility`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_SharedV4Warning_Message_"></a>
<a id="E2E_Decision_Sdk_SharedV4Warning_Message"></a>

### Message


```csharp
public string? Message { get; init; }
```


The message of the warning (`deprecated` and `other`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_SharedV4Warning_Setting_"></a>
<a id="E2E_Decision_Sdk_SharedV4Warning_Setting"></a>

### Setting


```csharp
public string? Setting { get; init; }
```


The deprecated setting or feature name (`deprecated`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Sdk_SharedV4Warning_Type_"></a>
<a id="E2E_Decision_Sdk_SharedV4Warning_Type"></a>

### Type


```csharp
public required string Type { get; init; }
```


The kind of warning: `unsupported`, `compatibility`, `deprecated`, or `other`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
