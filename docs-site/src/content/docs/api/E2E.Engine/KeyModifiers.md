---
title: "KeyModifiers"
description: "The names of KeyModifier, which a recorded action and an MCP argument spell. Upstream's KEYMODIFIERS and isKeyModifier."
editUrl: false
prev: false
next: false
sidebar:
  label: "KeyModifiers"
  hidden: true
---

<a id="E2E_Engine_KeyModifiers"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public static class KeyModifiers
```


The names of [KeyModifier](/api/E2E.Engine/KeyModifier/), which a recorded action and an MCP argument spell. Upstream's `KEY_MODIFIERS` and `isKeyModifier`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/KeyModifier.cs#L21)

## Properties

<a id="E2E_Engine_KeyModifiers_Names_"></a>
<a id="E2E_Engine_KeyModifiers_Names"></a>

### Names


```csharp
public static IReadOnlyList<string> Names { get; }
```


The names in declaration order: `Shift`, `Control`, `Alt`, `Meta`, `ControlOrMeta`.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/KeyModifier.cs#L24)

## Methods

<a id="E2E_Engine_KeyModifiers_TryParse_"></a>
<a id="E2E_Engine_KeyModifiers_TryParse_System_String_E2E_Engine_KeyModifier__"></a>

### TryParse(string?, out KeyModifier)


```csharp
public static bool TryParse(string? name, out KeyModifier modifier)
```


Reads a name exactly as spelled: no other case, no number.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `modifier` | [KeyModifier](/api/E2E.Engine/KeyModifier/) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/KeyModifier.cs#L29)
