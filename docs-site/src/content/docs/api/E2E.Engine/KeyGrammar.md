---
title: "KeyGrammar"
description: ".NET-only holder name (C# has no free functions): upstream's KEY_MODIFIERS, KEY_NAMES and parseKey in engine/contract.ts. The key grammar of press, shared by..."
editUrl: false
prev: false
next: false
sidebar:
  label: "KeyGrammar"
  hidden: true
---

<a id="E2E_Engine_KeyGrammar"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public static class KeyGrammar
```


.NET-only holder name (C# has no free functions): upstream's `KEY_MODIFIERS`, `KEY_NAMES` and `parseKey` in `engine/contract.ts`.
The key grammar of `press`, shared by every engine so a test's key names are portable: zero or more modifiers
and one key, joined by `+`. A modifier is `Shift`, `Control`, `Alt`, `Meta`, or
`ControlOrMeta`. A named key is one of [KeyNames](#E2E_Engine_KeyGrammar_KeyNames). Any single printable character is typed as itself.
The harness refuses a key outside it with `INVALID_ARGUMENT` (`Keys.RequireKey`); an engine without a grammar-valid key refuses it with `UNSUPPORTED_CAPABILITY`. It never reinterprets the spelling.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/KeyGrammar.cs)

## Properties

<a id="E2E_Engine_KeyGrammar_KeyModifiers_"></a>
<a id="E2E_Engine_KeyGrammar_KeyModifiers"></a>

### KeyModifiers


```csharp
public static IReadOnlyList<string> KeyModifiers { get; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_KeyGrammar_KeyNames_"></a>
<a id="E2E_Engine_KeyGrammar_KeyNames"></a>

### KeyNames


```csharp
public static IReadOnlyList<string> KeyNames { get; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

## Methods

<a id="E2E_Engine_KeyGrammar_ParseKey_"></a>
<a id="E2E_Engine_KeyGrammar_ParseKey_System_String_"></a>

### ParseKey(string)


```csharp
public static ParsedKey? ParseKey(string key)
```


Parses a `press` key against the grammar. Null when it does not conform: an unknown name, a repeated
modifier, a bare modifier, or several characters. A literal `+` is the character after the last separator,
so `Shift++` presses `+` with Shift held.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ParsedKey](/api/E2E.Engine/ParsedKey/)
