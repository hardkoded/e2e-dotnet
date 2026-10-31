---
title: "LocatorAction.SetInputFiles"
description: "Sets the files of a file input, or of a control that opens a file chooser when clicked, such as a button over a hidden input. <code class=\"paramref\">Paths</c..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.SetInputFiles"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_SetInputFiles"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.SetInputFiles : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.SetInputFiles>
```


Sets the files of a file input, or of a control that opens a file chooser when clicked, such as a button over a hidden input.
&lt;code class="paramref"&gt;Paths&lt;/code&gt; are absolute: the locator resolves them from the project root.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.SetInputFiles&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L45)

## Constructors

<a id="E2E_Engine_LocatorAction_SetInputFiles__ctor_"></a>
<a id="E2E_Engine_LocatorAction_SetInputFiles__ctor_System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `SetInputFiles(IReadOnlyList<string>)`


```csharp
public SetInputFiles(IReadOnlyList<string> Paths)
```


Sets the files of a file input, or of a control that opens a file chooser when clicked, such as a button over a hidden input.
&lt;code class="paramref"&gt;Paths&lt;/code&gt; are absolute: the locator resolves them from the project root.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Paths` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L45)

## Properties

<a id="E2E_Engine_LocatorAction_SetInputFiles_Paths_"></a>
<a id="E2E_Engine_LocatorAction_SetInputFiles_Paths"></a>

### Paths


```csharp
public IReadOnlyList<string> Paths { get; init; }
```


**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L45)
