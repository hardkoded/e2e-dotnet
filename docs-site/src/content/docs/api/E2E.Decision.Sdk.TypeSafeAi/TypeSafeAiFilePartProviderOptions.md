---
title: "TypeSafeAiFilePartProviderOptions"
description: "The AI SDK's typesafeAiFilePartProviderOptions: the typesafe options of an image file part."
editUrl: false
prev: false
next: false
sidebar:
  label: "TypeSafeAiFilePartProviderOptions"
  hidden: true
---

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiFilePartProviderOptions"></a>

Namespace: [E2E.Decision.Sdk.TypeSafeAi](/api/E2E.Decision.Sdk.TypeSafeAi/)


```csharp
public sealed class TypeSafeAiFilePartProviderOptions
```


The AI SDK's `typesafeAiFilePartProviderOptions`: the `typesafe` options of an image file part.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/TypeSafeAi/TypeSafeAiDecisionModelOptions.cs)

## Properties

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiFilePartProviderOptions_InsertReference_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiFilePartProviderOptions_InsertReference"></a>

### InsertReference


```csharp
public bool? InsertReference { get; init; }
```


Whether to insert the reference into state at the file's position. `false` sends the image only where you reference it in
instructions or criteria. `true` inserts it even when the state already contains it. `false` requires an explicit
[Reference](#E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiFilePartProviderOptions_Reference). Defaults to inserting the reference unless the state already contains it.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiFilePartProviderOptions_Reference_"></a>
<a id="E2E_Decision_Sdk_TypeSafeAi_TypeSafeAiFilePartProviderOptions_Reference"></a>

### Reference


```csharp
public string? Reference { get; init; }
```


Asset reference for this image. Inserted into state at the file's position unless the state already contains it. Every occurrence in
state, instructions, or criteria sends another copy of the image. Defaults to a generated ID. References must be nonempty and unique within
the request, and should not occur accidentally in other request text.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
