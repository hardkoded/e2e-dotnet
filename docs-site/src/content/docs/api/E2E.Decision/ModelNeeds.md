---
title: "ModelNeeds"
description: "What a factory needs from its decision model, and how its errors name the fix (upstream's ModelNeeds)."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelNeeds"
  hidden: true
---

<a id="E2E_Decision_ModelNeeds"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record ModelNeeds : IEquatable<ModelNeeds>
```


What a factory needs from its decision model, and how its errors name the fix (upstream's `ModelNeeds`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ModelNeeds&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Model.cs)

## Constructors

<a id="E2E_Decision_ModelNeeds__ctor_"></a>
<a id="E2E_Decision_ModelNeeds__ctor_System_String_System_String_System_String_"></a>

### ModelNeeds(string, string, string)


```csharp
public ModelNeeds(string Caller, string QuestionType, string Example)
```


What a factory needs from its decision model, and how its errors name the fix (upstream's `ModelNeeds`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Caller` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The factory, as every error names it, such as `DecisionExecutor`. |
| `QuestionType` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The question type the factory asks: `choice` or `boolean`. |
| `Example` | [string](https://learn.microsoft.com/dotnet/api/system.string) | How a valid model reads, ending the errors about a value that is no decision model. |

## Properties

<a id="E2E_Decision_ModelNeeds_Caller_"></a>
<a id="E2E_Decision_ModelNeeds_Caller"></a>

### Caller


```csharp
public string Caller { get; init; }
```


The factory, as every error names it, such as `DecisionExecutor`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_ModelNeeds_Example_"></a>
<a id="E2E_Decision_ModelNeeds_Example"></a>

### Example


```csharp
public string Example { get; init; }
```


How a valid model reads, ending the errors about a value that is no decision model.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_ModelNeeds_QuestionType_"></a>
<a id="E2E_Decision_ModelNeeds_QuestionType"></a>

### QuestionType


```csharp
public string QuestionType { get; init; }
```


The question type the factory asks: `choice` or `boolean`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
