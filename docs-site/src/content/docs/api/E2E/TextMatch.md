---
title: "TextMatch"
description: "A string or a regular expression, the upstream TextMatch, used by both screen queries and expectations. In a query, a string matches exactly by default, and..."
editUrl: false
prev: false
next: false
sidebar:
  label: "TextMatch"
  hidden: true
---

<a id="E2E_TextMatch"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class TextMatch
```


A string or a regular expression, the upstream `TextMatch`, used by
both screen queries and expectations. In a query, a string matches exactly
by default, and `exact: false` makes it a case-insensitive substring;
a [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) is tested against the whitespace-normalized text and
ignores `exact`. Converts implicitly from both.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L18)

## Properties

<a id="E2E_TextMatch_Pattern_"></a>
<a id="E2E_TextMatch_Pattern"></a>

### Pattern


```csharp
public Regex? Pattern { get; }
```


The regular expression to test, or null for a string.

**Value:** [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L30)

<a id="E2E_TextMatch_Text_"></a>
<a id="E2E_TextMatch_Text"></a>

### Text


```csharp
public string? Text { get; }
```


The string to match, or null for a regular expression.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L27)

## Methods

<a id="E2E_TextMatch_FromRegex_"></a>
<a id="E2E_TextMatch_FromRegex_System_Text_RegularExpressions_Regex_"></a>

### FromRegex(Regex)


```csharp
public static TextMatch FromRegex(Regex pattern)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pattern` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |

**Returns:** [TextMatch](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L44)

<a id="E2E_TextMatch_FromString_"></a>
<a id="E2E_TextMatch_FromString_System_String_"></a>

### FromString(string)


```csharp
public static TextMatch FromString(string text)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [TextMatch](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L38)

<a id="E2E_TextMatch_ToString_"></a>
<a id="E2E_TextMatch_ToString"></a>

### ToString()


```csharp
public override string ToString()
```


Returns a string that represents the current object.

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string) — A string that represents the current object.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L50)

## Operators

<a id="E2E_TextMatch_op_Implicit_"></a>
<a id="E2E_TextMatch_op_Implicit_System_String__E2E_TextMatch"></a>

### implicit operator TextMatch?(string?)


```csharp
public static implicit operator TextMatch?(string? text)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [TextMatch](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L32)

<a id="E2E_TextMatch_op_Implicit_System_Text_RegularExpressions_Regex__E2E_TextMatch"></a>

### implicit operator TextMatch?(Regex?)


```csharp
public static implicit operator TextMatch?(Regex? pattern)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `pattern` | [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) |  |

**Returns:** [TextMatch](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L35)
