---
title: "DrawnFieldAnswer"
description: "The answer of DrawnField: the drawn field and the string to enter in it."
editUrl: false
prev: false
next: false
sidebar:
  label: "DrawnFieldAnswer"
  hidden: true
---

<a id="E2E_Decision_DrawnFieldAnswer"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record DrawnFieldAnswer : IEquatable<DrawnFieldAnswer>
```


The answer of [DrawnField](/api/E2E.Decision/Text/#E2E_Decision_Text_DrawnField): the drawn field and the string to enter in it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;DrawnFieldAnswer&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Text.cs)

## Constructors

<a id="E2E_Decision_DrawnFieldAnswer__ctor_"></a>
<a id="E2E_Decision_DrawnFieldAnswer__ctor_System_String_System_String_"></a>

### DrawnFieldAnswer(string, string?)


```csharp
public DrawnFieldAnswer(string Target, string? Text)
```


The answer of [DrawnField](/api/E2E.Decision/Text/#E2E_Decision_Text_DrawnField): the drawn field and the string to enter in it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Target` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_DrawnFieldAnswer_Target_"></a>
<a id="E2E_Decision_DrawnFieldAnswer_Target"></a>

### Target


```csharp
public string Target { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_DrawnFieldAnswer_Text_"></a>
<a id="E2E_Decision_DrawnFieldAnswer_Text"></a>

### Text


```csharp
public string? Text { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
