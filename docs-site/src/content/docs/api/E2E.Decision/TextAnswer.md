---
title: "TextAnswer"
description: "The answer of TextAsk: the string to enter, or null when the goal supplies none."
editUrl: false
prev: false
next: false
sidebar:
  label: "TextAnswer"
  hidden: true
---

<a id="E2E_Decision_TextAnswer"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed record TextAnswer : IEquatable<TextAnswer>
```


The answer of [TextAsk](/api/E2E.Decision/Text/#E2E_Decision_Text_TextAsk): the string to enter, or null when the goal supplies none.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;TextAnswer&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Text.cs)

## Constructors

<a id="E2E_Decision_TextAnswer__ctor_"></a>
<a id="E2E_Decision_TextAnswer__ctor_System_String_"></a>

### TextAnswer(string?)


```csharp
public TextAnswer(string? Text)
```


The answer of [TextAsk](/api/E2E.Decision/Text/#E2E_Decision_Text_TextAsk): the string to enter, or null when the goal supplies none.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Decision_TextAnswer_Text_"></a>
<a id="E2E_Decision_TextAnswer_Text"></a>

### Text


```csharp
public string? Text { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
