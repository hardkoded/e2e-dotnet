---
title: "DecisionQuestion"
description: "One decision question (the AI SDK's DecisionModelV4Question). A choice question carries its options, a score question its ordered levels, and a boolean quest..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DecisionQuestion"
  hidden: true
---

<a id="E2E_Decision_Sdk_DecisionQuestion"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed class DecisionQuestion
```


One decision question (the AI SDK's `DecisionModelV4Question`). A `choice` question carries its options, a
`score` question its ordered levels, and a `boolean` question instructions or at least one criterion.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/DecisionTypes.cs)

## Properties

<a id="E2E_Decision_Sdk_DecisionQuestion_Choices_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestion_Choices"></a>

### Choices


```csharp
public IReadOnlyList<DecisionChoice>? Choices { get; init; }
```


The options of a `choice` question, in order.

**Value:** [IReadOnlyList&lt;DecisionChoice&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_DecisionQuestion_FalseCriteria_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestion_FalseCriteria"></a>

### FalseCriteria


```csharp
public DecisionInput? FalseCriteria { get; init; }
```


What makes a `boolean` question false.

**Value:** [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/)

<a id="E2E_Decision_Sdk_DecisionQuestion_Instructions_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestion_Instructions"></a>

### Instructions


```csharp
public DecisionInput? Instructions { get; init; }
```


What to decide. Null when the criteria say it all.

**Value:** [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/)

<a id="E2E_Decision_Sdk_DecisionQuestion_Levels_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestion_Levels"></a>

### Levels


```csharp
public IReadOnlyList<DecisionInput?>? Levels { get; init; }
```


The descriptions of the ordered levels of a `score` question: level `i` is the `i`th entry.

**Value:** [IReadOnlyList&lt;DecisionInput&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Decision_Sdk_DecisionQuestion_NullCriteria_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestion_NullCriteria"></a>

### NullCriteria


```csharp
public bool NullCriteria { get; init; }
```


A `boolean` question whose `criteria` is an explicit JSON `null` (upstream's `criteria: null`). .NET-only: C# has no
way to tell it from absent criteria.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Decision_Sdk_DecisionQuestion_TrueCriteria_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestion_TrueCriteria"></a>

### TrueCriteria


```csharp
public DecisionInput? TrueCriteria { get; init; }
```


What makes a `boolean` question true.

**Value:** [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/)

<a id="E2E_Decision_Sdk_DecisionQuestion_Type_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestion_Type"></a>

### Type


```csharp
public required string Type { get; init; }
```


`choice`, `score`, `boolean`, or `box`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_Decision_Sdk_DecisionQuestion_Boolean_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestion_Boolean_E2E_Decision_Sdk_DecisionInput_E2E_Decision_Sdk_DecisionInput_E2E_Decision_Sdk_DecisionInput_"></a>

### Boolean(DecisionInput?, DecisionInput?, DecisionInput?)


```csharp
public static DecisionQuestion Boolean(DecisionInput? instructions, DecisionInput? trueCriteria = null, DecisionInput? falseCriteria = null)
```


A `boolean` question.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `instructions` | [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/) |  |
| `trueCriteria` | [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/) |  |
| `falseCriteria` | [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/) |  |

**Returns:** [DecisionQuestion](./)

<a id="E2E_Decision_Sdk_DecisionQuestion_Box_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestion_Box_E2E_Decision_Sdk_DecisionInput_"></a>

### Box(DecisionInput?)


```csharp
public static DecisionQuestion Box(DecisionInput? instructions)
```


A `box` question: where in an image something is.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `instructions` | [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/) |  |

**Returns:** [DecisionQuestion](./)

<a id="E2E_Decision_Sdk_DecisionQuestion_Choice_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestion_Choice_E2E_Decision_Sdk_DecisionInput_System_Collections_Generic_IReadOnlyList_E2E_Decision_Sdk_DecisionChoice__"></a>

### `Choice(DecisionInput?, IReadOnlyList<DecisionChoice>)`


```csharp
public static DecisionQuestion Choice(DecisionInput? instructions, IReadOnlyList<DecisionChoice> choices)
```


A `choice` question.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `instructions` | [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/) |  |
| `choices` | [IReadOnlyList&lt;DecisionChoice&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

**Returns:** [DecisionQuestion](./)

<a id="E2E_Decision_Sdk_DecisionQuestion_Score_"></a>
<a id="E2E_Decision_Sdk_DecisionQuestion_Score_E2E_Decision_Sdk_DecisionInput_System_Collections_Generic_IReadOnlyList_E2E_Decision_Sdk_DecisionInput__"></a>

### `Score(DecisionInput?, IReadOnlyList<DecisionInput?>)`


```csharp
public static DecisionQuestion Score(DecisionInput? instructions, IReadOnlyList<DecisionInput?> levels)
```


A `score` question over ordered levels.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `instructions` | [DecisionInput](/api/E2E.Decision.Sdk/DecisionInput/) |  |
| `levels` | [IReadOnlyList&lt;DecisionInput&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

**Returns:** [DecisionQuestion](./)
