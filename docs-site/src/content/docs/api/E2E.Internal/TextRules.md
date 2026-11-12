---
title: "TextRules"
description: "Whitespace and string matching used by locators. Exact string matches are case-sensitive after whitespace is collapsed. Inexact matches are case-insensitive..."
editUrl: false
prev: false
next: false
sidebar:
  label: "TextRules"
  hidden: true
---

<a id="E2E_Internal_TextRules"></a>

Namespace: [E2E.Internal](/api/E2E.Internal/)


```csharp
public static class TextRules
```


Whitespace and string matching used by locators. Exact string matches are
case-sensitive after whitespace is collapsed. Inexact matches are
case-insensitive substrings, which is how `exact: false` behaves upstream.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/obj/Release/net10.0/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs)

## Methods

<a id="E2E_Internal_TextRules_Bound_"></a>
<a id="E2E_Internal_TextRules_Bound_System_String_System_Int32_"></a>

### Bound(string, int)


```csharp
public static string Bound(string text, int maxChars)
```


Caps prose at &lt;code class="paramref"&gt;maxChars&lt;/code&gt;, marking the cut with an ellipsis. A cut never splits a surrogate pair.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `maxChars` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Internal_TextRules_Collapse_"></a>
<a id="E2E_Internal_TextRules_Collapse_System_String_"></a>

### Collapse(string)


```csharp
public static string Collapse(string text)
```


App text as one line, as upstream's `collapseText` reads it: whitespace collapsed, and a control
character becomes U+FFFD, so app text cannot put it in a prompt.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Internal_TextRules_Compare_"></a>
<a id="E2E_Internal_TextRules_Compare_System_String_E2E_TextMatch_System_Boolean_System_Boolean_System_Nullable_System_Boolean__"></a>

### Compare(string, TextMatch, bool, bool, bool?)


```csharp
public static bool Compare(string actual, TextMatch expected, bool contains, bool normalize, bool? ignoreCase)
```


The text matcher rule. A pattern is tested anywhere in the subject in both modes. A string
equals, or is contained by, the subject, folding case when &lt;code class="paramref"&gt;ignoreCase&lt;/code&gt; is true.
&lt;code class="paramref"&gt;normalize&lt;/code&gt; collapses whitespace on both sides first.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `actual` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `contains` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `normalize` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |
| `ignoreCase` | [bool?](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Internal_TextRules_Matches_"></a>
<a id="E2E_Internal_TextRules_Matches_System_String_System_String_System_Boolean_"></a>

### Matches(string?, string, bool)


```csharp
public static bool Matches(string? actual, string expected, bool exact)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `actual` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `expected` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Internal_TextRules_MatchesText_"></a>
<a id="E2E_Internal_TextRules_MatchesText_System_String_E2E_TextMatch_System_Boolean_"></a>

### MatchesText(string, TextMatch, bool)


```csharp
public static bool MatchesText(string actual, TextMatch expected, bool exact = true)
```


Matches normalized text against a pattern (upstream's `matchesText`): a string is compared exactly, or as a case-insensitive
substring when &lt;code class="paramref"&gt;exact&lt;/code&gt; is false; a regular expression tests the normalized text.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `actual` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `expected` | [TextMatch](/api/E2E/TextMatch/) |  |
| `exact` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Internal_TextRules_Normalize_"></a>
<a id="E2E_Internal_TextRules_Normalize_System_String_"></a>

### Normalize(string)


```csharp
public static string Normalize(string text)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Internal_TextRules_SanitizeText_"></a>
<a id="E2E_Internal_TextRules_SanitizeText_System_String_"></a>

### SanitizeText(string)


```csharp
public static string SanitizeText(string text)
```


Upstream's `sanitizeText` (`internal/errors.ts`): strips ANSI escape sequences, then every C0 and C1
control character except tab and newline becomes U+FFFD. Libraries color their own messages; left in, the color codes
reach a report as literal text.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Internal_TextRules_TruncateUtf8_"></a>
<a id="E2E_Internal_TextRules_TruncateUtf8_System_String_System_Int32_"></a>

### TruncateUtf8(string, int)


```csharp
public static string TruncateUtf8(string text, int maxBytes)
```


Upstream's `truncateUtf8`: cuts the text so its UTF-8 encoding fits &lt;code class="paramref"&gt;maxBytes&lt;/code&gt;, never inside a character.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `maxBytes` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Internal_TextRules_WithHint_"></a>
<a id="E2E_Internal_TextRules_WithHint_System_String_System_String_"></a>

### WithHint(string, string)


```csharp
public static string WithHint(string message, string hint)
```


Upstream's `withHint`: appends a remedy to a message, as a clause after a one-line message and on its own line after a
multi-line one, so the hint never dangles off a sentence that already ended.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `hint` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)
