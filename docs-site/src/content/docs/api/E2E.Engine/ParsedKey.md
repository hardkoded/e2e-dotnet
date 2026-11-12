---
title: "ParsedKey"
description: "Upstream's ParsedKey (engine/contract.ts): one parsed press key: its modifiers, and either a named key or a single character."
editUrl: false
prev: false
next: false
sidebar:
  label: "ParsedKey"
  hidden: true
---

<a id="E2E_Engine_ParsedKey"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record ParsedKey : IEquatable<ParsedKey>
```


Upstream's `ParsedKey` (`engine/contract.ts`): one parsed `press` key: its modifiers, and either a named key or a single character.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ParsedKey&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/KeyGrammar.cs)

## Constructors

<a id="E2E_Engine_ParsedKey__ctor_"></a>
<a id="E2E_Engine_ParsedKey__ctor_System_Collections_Generic_IReadOnlyList_System_String__System_String_System_String_"></a>

### `ParsedKey(IReadOnlyList<string>, string?, string?)`


```csharp
public ParsedKey(IReadOnlyList<string> Modifiers, string? Name, string? Character)
```


Upstream's `ParsedKey` (`engine/contract.ts`): one parsed `press` key: its modifiers, and either a named key or a single character.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Modifiers` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Character` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Engine_ParsedKey_Character_"></a>
<a id="E2E_Engine_ParsedKey_Character"></a>

### Character


```csharp
public string? Character { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_ParsedKey_Modifiers_"></a>
<a id="E2E_Engine_ParsedKey_Modifiers"></a>

### Modifiers


```csharp
public IReadOnlyList<string> Modifiers { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_ParsedKey_Name_"></a>
<a id="E2E_Engine_ParsedKey_Name"></a>

### Name


```csharp
public string? Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
