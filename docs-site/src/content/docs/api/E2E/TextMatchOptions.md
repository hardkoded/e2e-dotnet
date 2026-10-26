---
title: "TextMatchOptions"
description: "Options shared by the text-matching queries."
editUrl: false
prev: false
next: false
sidebar:
  label: "TextMatchOptions"
  hidden: true
---

<a id="E2E_TextMatchOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public class TextMatchOptions
```


Options shared by the text-matching queries.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Derived:** [RoleOptions](/api/E2E/RoleOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L112)

## Properties

<a id="E2E_TextMatchOptions_Exact_"></a>
<a id="E2E_TextMatchOptions_Exact"></a>

### Exact


```csharp
public bool Exact { get; init; }
```


Exact match, the default. `false` is a case-insensitive substring. A [Regex](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex) ignores it.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L115)

<a id="E2E_TextMatchOptions_Visible_"></a>
<a id="E2E_TextMatchOptions_Visible"></a>

### Visible


```csharp
public bool Visible { get; init; }
```


Drops nodes the platform reports as hidden before the exactly-one rule,
so a visible node with a hidden twin still resolves. On the web, hidden is
what renders: `display: none`, a `visibility` other than
`visible`, and content under `content-visibility: hidden` or a
closed `details`; a child that sets `visibility: visible` under a
hidden parent is visible, and an `aria-hidden` node that paints is visible.
Role queries never match hidden or `aria-hidden` nodes, whatever this says.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/TextMatch.cs#L126)
