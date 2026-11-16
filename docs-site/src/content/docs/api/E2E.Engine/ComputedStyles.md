---
title: "ComputedStyles"
description: "Upstream's engine/computed-style.ts (.NET-only holder name: C# has no free functions, and ComputedStyle is the tests' namespace): the rendered node propertie..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ComputedStyles"
  hidden: true
---

<a id="E2E_Engine_ComputedStyles"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public static class ComputedStyles
```


Upstream's `engine/computed-style.ts` (.NET-only holder name: C# has no free functions, and `ComputedStyle` is the tests' namespace): the rendered node properties an engine's `readStyle` may be asked for. A property name
is a plain string (upstream's `ComputedStyleProperty` union of literals), and the values an engine returns are computed CSS strings
keyed by the properties requested (upstream's `ComputedStyleValues`, here `IReadOnlyDictionary&lt;T&gt;`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ComputedStyle.cs)

## Properties

<a id="E2E_Engine_ComputedStyles_ComputedStyleProperties_"></a>
<a id="E2E_Engine_ComputedStyles_ComputedStyleProperties"></a>

### ComputedStyleProperties


```csharp
public static IReadOnlyList<string> ComputedStyleProperties { get; }
```


Upstream's `COMPUTED_STYLE_PROPERTIES`: the CSS properties whose computed values contain presentation data rather than URLs or
generated content, in kebab case.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

## Methods

<a id="E2E_Engine_ComputedStyles_ValidateComputedStyleProperties_"></a>
<a id="E2E_Engine_ComputedStyles_ValidateComputedStyleProperties_System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `ValidateComputedStyleProperties(IReadOnlyList<string>?)`


```csharp
public static IReadOnlyList<string> ValidateComputedStyleProperties(IReadOnlyList<string>? properties)
```


Upstream's `validateComputedStyleProperties`: checks, deduplicates, and copies the requested properties before any engine or page
access. An empty list, or a property outside [ComputedStyleProperties](#E2E_Engine_ComputedStyles_ComputedStyleProperties), is `INVALID_ARGUMENT`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `properties` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The requested property names. |

**Returns:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
