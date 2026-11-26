---
title: "WireSetInputFilePayloads"
description: "Upstream's WireLocatorAction for setInputFilePayloads: file bytes, like snapshot pixels, cross as base64."
editUrl: false
prev: false
next: false
sidebar:
  label: "WireSetInputFilePayloads"
  hidden: true
---

<a id="E2E_Engine_Protocol_WireSetInputFilePayloads"></a>

Namespace: [E2E.Engine.Protocol](/api/E2E.Engine.Protocol/)


```csharp
public sealed record WireSetInputFilePayloads : IEquatable<WireSetInputFilePayloads>
```


Upstream's `WireLocatorAction` for `setInputFilePayloads`: file bytes, like snapshot pixels, cross as base64.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;WireSetInputFilePayloads&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Protocol/Protocol.cs)

## Constructors

<a id="E2E_Engine_Protocol_WireSetInputFilePayloads__ctor_"></a>
<a id="E2E_Engine_Protocol_WireSetInputFilePayloads__ctor_System_Collections_Generic_IReadOnlyList_E2E_Engine_Protocol_WireFilePayload__"></a>

### `WireSetInputFilePayloads(IReadOnlyList<WireFilePayload>)`


```csharp
public WireSetInputFilePayloads(IReadOnlyList<WireFilePayload> Files)
```


Upstream's `WireLocatorAction` for `setInputFilePayloads`: file bytes, like snapshot pixels, cross as base64.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Files` | [IReadOnlyList&lt;WireFilePayload&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |

## Properties

<a id="E2E_Engine_Protocol_WireSetInputFilePayloads_Files_"></a>
<a id="E2E_Engine_Protocol_WireSetInputFilePayloads_Files"></a>

### Files


```csharp
public IReadOnlyList<WireFilePayload> Files { get; init; }
```


**Value:** [IReadOnlyList&lt;WireFilePayload&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
