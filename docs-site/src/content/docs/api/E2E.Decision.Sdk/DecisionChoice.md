---
title: "DecisionChoice"
description: "One option of a choice question."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionChoice"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionChoice"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record DecisionChoice : IEquatable<DecisionChoice>
```


One option of a `choice` question.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DecisionChoice&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Constructors

<a id="E2E_Decision_Sdk_DecisionChoice__ctor_"></a>
<a id="E2E_Decision_Sdk_DecisionChoice__ctor_System_String_E2E_Decision_Sdk_DecisionInput_"></a>

### DecisionChoice(string, DecisionInput?)


```csharp
public DecisionChoice(string Value, DecisionInput? Description = null)
```


One option of a `choice` question.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Description` | [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/) |  |

## Properties

<a id="E2E_Decision_Sdk_DecisionChoice_Description_"></a>
<a id="E2E_Decision_Sdk_DecisionChoice_Description"></a>

### Description


```csharp
public DecisionInput? Description { get; init; }
```


**Value:** [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/)

<a id="E2E_Decision_Sdk_DecisionChoice_Value_"></a>
<a id="E2E_Decision_Sdk_DecisionChoice_Value"></a>

### Value


```csharp
public string Value { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
