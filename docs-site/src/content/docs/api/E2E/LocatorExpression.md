---
title: "LocatorExpression"
description: "A locator expression. Query kinds (role, text, label, testid, placeholder, displayValue) search the descendants of Parent when it is set. filter and index na..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorExpression"
  hidden: true
---

<a id="E2E_LocatorExpression"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record LocatorExpression : IEquatable<LocatorExpression>
```


A locator expression. Query kinds (`role`, `text`, `label`,
`testid`, `placeholder`, `displayValue`) search the
descendants of [Parent](#E2E_LocatorExpression_Parent) when it is set. `filter` and
`index` narrow [Source](#E2E_LocatorExpression_Source). `union` matches either
[Left](#E2E_LocatorExpression_Left) or [Right](#E2E_LocatorExpression_Right), each node once, in document order. `selector` is a CSS or XPath selector in the
engine's own language, and `frame` scopes [Source](#E2E_LocatorExpression_Source) into the iframe [Selector](#E2E_LocatorExpression_Selector) names.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;LocatorExpression&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1303)

## Fields

<a id="E2E_LocatorExpression_LastIndex"></a>

### LastIndex


```csharp
public const int LastIndex = -1
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1305)

## Properties

<a id="E2E_LocatorExpression_Checked_"></a>
<a id="E2E_LocatorExpression_Checked"></a>

### Checked


```csharp
public bool? Checked { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1322)

<a id="E2E_LocatorExpression_Disabled_"></a>
<a id="E2E_LocatorExpression_Disabled"></a>

### Disabled


```csharp
public bool? Disabled { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1324)

<a id="E2E_LocatorExpression_Exact_"></a>
<a id="E2E_LocatorExpression_Exact"></a>

### Exact


```csharp
public bool Exact { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1318)

<a id="E2E_LocatorExpression_Expanded_"></a>
<a id="E2E_LocatorExpression_Expanded"></a>

### Expanded


```csharp
public bool? Expanded { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1328)

<a id="E2E_LocatorExpression_Has_"></a>
<a id="E2E_LocatorExpression_Has"></a>

### Has


```csharp
public LocatorExpression? Has { get; init; }
```


**Value:** [LocatorExpression](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1353)

<a id="E2E_LocatorExpression_HasNot_"></a>
<a id="E2E_LocatorExpression_HasNot"></a>

### HasNot


```csharp
public LocatorExpression? HasNot { get; init; }
```


Upstream's `hasNot`: drops a match whose subtree resolves the inner expression.

**Value:** [LocatorExpression](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1356)

<a id="E2E_LocatorExpression_HasNotText_"></a>
<a id="E2E_LocatorExpression_HasNotText"></a>

### HasNotText


```csharp
public TextMatch? HasNotText { get; init; }
```


Upstream's `hasNotText`: drops a match whose subtree text matches. A case-insensitive substring unless [HasNotTextExact](#E2E_LocatorExpression_HasNotTextExact).

**Value:** [TextMatch](/api/E2E/TextMatch/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1345)

<a id="E2E_LocatorExpression_HasNotTextExact_"></a>
<a id="E2E_LocatorExpression_HasNotTextExact"></a>

### HasNotTextExact


```csharp
public bool HasNotTextExact { get; init; }
```


Whether [HasNotText](#E2E_LocatorExpression_HasNotText) must equal the text instead of being a case-insensitive substring of it. .NET-only, as
[HasTextExact](#E2E_LocatorExpression_HasTextExact) is: [TextMatch](/api/E2E/TextMatch/) holds no exactness of its own.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1351)

<a id="E2E_LocatorExpression_HasText_"></a>
<a id="E2E_LocatorExpression_HasText"></a>

### HasText


```csharp
public TextMatch? HasText { get; init; }
```


**Value:** [TextMatch](/api/E2E/TextMatch/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1334)

<a id="E2E_LocatorExpression_HasTextExact_"></a>
<a id="E2E_LocatorExpression_HasTextExact"></a>

### HasTextExact


```csharp
public bool HasTextExact { get; init; }
```


Whether [HasText](#E2E_LocatorExpression_HasText) must equal the text instead of being a case-insensitive substring of it, as upstream's text pattern
carries its `exact`. [Filter(TextMatch)](/api/E2E/Locator/#E2E_Locator_Filter_E2E_TextMatch_) builds a substring match (upstream's `{ exact: false }`), so
it leaves this `false`. A [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) ignores it. .NET-only: [TextMatch](/api/E2E/TextMatch/) holds no
exactness of its own.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1342)

<a id="E2E_LocatorExpression_Index_"></a>
<a id="E2E_LocatorExpression_Index"></a>

### Index


```csharp
public int? Index { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1358)

<a id="E2E_LocatorExpression_Kind_"></a>
<a id="E2E_LocatorExpression_Kind"></a>

### Kind


```csharp
public string Kind { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1311)

<a id="E2E_LocatorExpression_Left_"></a>
<a id="E2E_LocatorExpression_Left"></a>

### Left


```csharp
public LocatorExpression? Left { get; init; }
```


**Value:** [LocatorExpression](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1360)

<a id="E2E_LocatorExpression_Level_"></a>
<a id="E2E_LocatorExpression_Level"></a>

### Level


```csharp
public int? Level { get; init; }
```


**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1332)

<a id="E2E_LocatorExpression_Parent_"></a>
<a id="E2E_LocatorExpression_Parent"></a>

### Parent


```csharp
public LocatorExpression? Parent { get; init; }
```


**Value:** [LocatorExpression](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1307)

<a id="E2E_LocatorExpression_Pressed_"></a>
<a id="E2E_LocatorExpression_Pressed"></a>

### Pressed


```csharp
public bool? Pressed { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1330)

<a id="E2E_LocatorExpression_Right_"></a>
<a id="E2E_LocatorExpression_Right"></a>

### Right


```csharp
public LocatorExpression? Right { get; init; }
```


**Value:** [LocatorExpression](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1362)

<a id="E2E_LocatorExpression_Role_"></a>
<a id="E2E_LocatorExpression_Role"></a>

### Role


```csharp
public string? Role { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1313)

<a id="E2E_LocatorExpression_Selected_"></a>
<a id="E2E_LocatorExpression_Selected"></a>

### Selected


```csharp
public bool? Selected { get; init; }
```


**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1326)

<a id="E2E_LocatorExpression_Selector_"></a>
<a id="E2E_LocatorExpression_Selector"></a>

### Selector


```csharp
public string? Selector { get; init; }
```


The expression of a `selector` query, in the engine's own selector language (a device engine's, or the web engine's CSS and
XPath), or the selector of the iframe a `frame` expression scopes [Source](#E2E_LocatorExpression_Source) into.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1368)

<a id="E2E_LocatorExpression_Source_"></a>
<a id="E2E_LocatorExpression_Source"></a>

### Source


```csharp
public LocatorExpression? Source { get; init; }
```


**Value:** [LocatorExpression](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1309)

<a id="E2E_LocatorExpression_Value_"></a>
<a id="E2E_LocatorExpression_Value"></a>

### Value


```csharp
public TextMatch? Value { get; init; }
```


The role query's name, or the value every other query kind matches.

**Value:** [TextMatch](/api/E2E/TextMatch/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1316)

<a id="E2E_LocatorExpression_Visible_"></a>
<a id="E2E_LocatorExpression_Visible"></a>

### Visible


```csharp
public bool Visible { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1320)

## Methods

<a id="E2E_LocatorExpression_Describe_"></a>
<a id="E2E_LocatorExpression_Describe"></a>

### Describe()


```csharp
public string Describe()
```


**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs#L1443)
