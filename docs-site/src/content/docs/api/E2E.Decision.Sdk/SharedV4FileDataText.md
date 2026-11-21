---
title: "SharedV4FileDataText"
description: "File data holding inline text content, such as an inline text document."
editUrl: false
prev: false
next: false
sidebar:
  label: "SharedV4FileDataText"
  hidden: true
---

<a id="E2E_Decision_Sdk_SharedV4FileDataText"></a>

Namespace: [E2E.Decision.Sdk](/api/E2E.Decision.Sdk/)


```csharp
public sealed record SharedV4FileDataText : SharedV4FileData, IEquatable<SharedV4FileData>, IEquatable<SharedV4FileDataText>
```


File data holding inline text content, such as an inline text document.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [SharedV4FileData](/api/E2E.Decision.Sdk/SharedV4FileData/)

**Implements:** [IEquatable&lt;SharedV4FileData&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;SharedV4FileDataText&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Sdk/SharedV4.cs)

## Constructors

<a id="E2E_Decision_Sdk_SharedV4FileDataText__ctor_"></a>
<a id="E2E_Decision_Sdk_SharedV4FileDataText__ctor_System_String_"></a>

### SharedV4FileDataText(string)


```csharp
public SharedV4FileDataText(string Text)
```


File data holding inline text content, such as an inline text document.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The text. |

## Properties

<a id="E2E_Decision_Sdk_SharedV4FileDataText_Text_"></a>
<a id="E2E_Decision_Sdk_SharedV4FileDataText_Text"></a>

### Text


```csharp
public string Text { get; init; }
```


The text.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
