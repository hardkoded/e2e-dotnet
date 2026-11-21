---
title: "DecisionTextPart"
description: "Plain text."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionTextPart"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionTextPart"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DecisionTextPart : DecisionStatePart, IEquatable<DecisionStatePart>, IEquatable<DecisionTextPart>
```


Plain text.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [DecisionStatePart](/api/E2E.Decision.Sdk/DecisionStatePart/)

**Implements:** [IEquatable&lt;DecisionStatePart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;DecisionTextPart&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionTextPart__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionTextPart__ctor_System_String_"></a>

### DecisionTextPart(string)


```csharp
public DecisionTextPart(string Text)
```


Plain text.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionTextPart_Text_"></a>
<a id="E2E_Decision_Sdk_DecisionTextPart_Text"></a>

### Text


```csharp
public string Text { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
