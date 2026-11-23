---
title: "LocatorAction.SetInputFilePayloads"
description: "Sets the files of a file input, or of a control that opens a file chooser, from memory. An empty Files clears the input. Upstream's setInputFilePayloads; onl..."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.SetInputFilePayloads"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_SetInputFilePayloads"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.SetInputFilePayloads : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.SetInputFilePayloads>
```


Sets the files of a file input, or of a control that opens a file chooser, from memory. An empty &lt;code class="paramref"&gt;Files&lt;/code&gt; clears the
input. Upstream's `setInputFilePayloads`; only an engine that declares that kind receives it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.SetInputFilePayloads&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs)

## Constructors

<a id="E2E_Engine_LocatorAction_SetInputFilePayloads__ctor_"></a>
<a id="E2E_Engine_LocatorAction_SetInputFilePayloads__ctor_System_Collections_Generic_IReadOnlyList_E2E_FilePayload__"></a>

### `SetInputFilePayloads(IReadOnlyList<FilePayload>)`


```csharp
public SetInputFilePayloads(IReadOnlyList<FilePayload> Files)
```


Sets the files of a file input, or of a control that opens a file chooser, from memory. An empty &lt;code class="paramref"&gt;Files&lt;/code&gt; clears the
input. Upstream's `setInputFilePayloads`; only an engine that declares that kind receives it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Files` | [IReadOnlyList&lt;FilePayload&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_Engine_LocatorAction_SetInputFilePayloads_Files_"></a>
<a id="E2E_Engine_LocatorAction_SetInputFilePayloads_Files"></a>

### Files


```csharp
public IReadOnlyList<FilePayload> Files { get; init; }
```


**Value:** [IReadOnlyList&lt;FilePayload&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
