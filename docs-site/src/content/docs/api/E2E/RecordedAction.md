---
title: "RecordedAction"
description: "RecordedAction in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "RecordedAction"
  hidden: true
---

<a id="E2E_RecordedAction"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class RecordedAction
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs)

## Properties

<a id="E2E_RecordedAction_Destination_"></a>
<a id="E2E_RecordedAction_Destination"></a>

### Destination


```csharp
public RecordedTarget? Destination { get; set; }
```


Upstream's `destination` of a `drag`: the node the source was dropped on, described like the source with the container it
sat in. A replay re-finds both ends on one screen before it drags. Null on every other action.

**Value:** [RecordedTarget](/api/E2E/RecordedTarget/)

<a id="E2E_RecordedAction_Direction_"></a>
<a id="E2E_RecordedAction_Direction"></a>

### Direction


```csharp
public string? Direction { get; set; }
```


`up`, `down`, `left`, or `right` for `scroll` and `scrollUntil`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Key_"></a>
<a id="E2E_RecordedAction_Key"></a>

### Key


```csharp
public string? Key { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Kind_"></a>
<a id="E2E_RecordedAction_Kind"></a>

### Kind


```csharp
public string Kind { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Modifiers_"></a>
<a id="E2E_RecordedAction_Modifiers"></a>

### Modifiers


```csharp
public List<string>? Modifiers { get; set; }
```


Keys a `tap`, `doubleTap`, or `secondaryTap` held for the click, replayed with it. Null holds none.

**Value:** [List&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)

<a id="E2E_RecordedAction_Name_"></a>
<a id="E2E_RecordedAction_Name"></a>

### Name


```csharp
public string? Name { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Paths_"></a>
<a id="E2E_RecordedAction_Paths"></a>

### Paths


```csharp
public List<string>? Paths { get; set; }
```


Upstream's `paths` of an `upload`: the files attached, by project-relative path as the executor gave them. A replay input like a
typed value, expanded with the call's params and authorized against the project root again before it reaches the engine.

**Value:** [List&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)

<a id="E2E_RecordedAction_Role_"></a>
<a id="E2E_RecordedAction_Role"></a>

### Role


```csharp
public string? Role { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Spans_"></a>
<a id="E2E_RecordedAction_Spans"></a>

### Spans


```csharp
public double? Spans { get; set; }
```


Upstream's `spans`: the share of the viewport the scrolled list covered when recorded, 0 to 1. A list that filled the screen
(`MainListShare`) scrolls as the viewport does when a replay cannot re-find it; a smaller region never does.
Null for a scroll with no list, and for a list on an engine that does not measure the viewport.

**Value:** [double?](https://learn.microsoft.com/dotnet/api/system.double)

<a id="E2E_RecordedAction_TestId_"></a>
<a id="E2E_RecordedAction_TestId"></a>

### TestId


```csharp
public string? TestId { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Text_"></a>
<a id="E2E_RecordedAction_Text"></a>

### Text


```csharp
public string? Text { get; set; }
```


The text a `scrollUntil` paged toward.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Times_"></a>
<a id="E2E_RecordedAction_Times"></a>

### Times


```csharp
public int? Times { get; set; }
```


Consecutive identical scrolls folded into one action. Null means one.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_RecordedAction_Url_"></a>
<a id="E2E_RecordedAction_Url"></a>

### Url


```csharp
public string? Url { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Value_"></a>
<a id="E2E_RecordedAction_Value"></a>

### Value


```csharp
public string? Value { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_RecordedAction_Within_"></a>
<a id="E2E_RecordedAction_Within"></a>

### Within


```csharp
public string? Within { get; set; }
```


Upstream's `within`: the key of the container the target sat in (a row's first cell), with secrets redacted. Relocation
requires the same key, so one row's "Delete" is never taken for another's. Null when the target sat in no named container.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
