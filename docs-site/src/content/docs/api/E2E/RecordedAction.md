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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L122)

## Properties

<a id="E2E_RecordedAction_Direction_"></a>
<a id="E2E_RecordedAction_Direction"></a>

### Direction


```csharp
public string? Direction { get; set; }
```


`up`, `down`, `left`, or `right` for `scroll` and `scrollUntil`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L139)

<a id="E2E_RecordedAction_Key_"></a>
<a id="E2E_RecordedAction_Key"></a>

### Key


```csharp
public string? Key { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L134)

<a id="E2E_RecordedAction_Kind_"></a>
<a id="E2E_RecordedAction_Kind"></a>

### Kind


```csharp
public string Kind { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L124)

<a id="E2E_RecordedAction_Modifiers_"></a>
<a id="E2E_RecordedAction_Modifiers"></a>

### Modifiers


```csharp
public List<string>? Modifiers { get; set; }
```


Keys a `tap` or `doubleTap` held for the click, replayed with it. Null holds none.

**Value:** [List&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L158)

<a id="E2E_RecordedAction_Name_"></a>
<a id="E2E_RecordedAction_Name"></a>

### Name


```csharp
public string? Name { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L128)

<a id="E2E_RecordedAction_Role_"></a>
<a id="E2E_RecordedAction_Role"></a>

### Role


```csharp
public string? Role { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L126)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L152)

<a id="E2E_RecordedAction_TestId_"></a>
<a id="E2E_RecordedAction_TestId"></a>

### TestId


```csharp
public string? TestId { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L130)

<a id="E2E_RecordedAction_Text_"></a>
<a id="E2E_RecordedAction_Text"></a>

### Text


```csharp
public string? Text { get; set; }
```


The text a `scrollUntil` paged toward.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L145)

<a id="E2E_RecordedAction_Times_"></a>
<a id="E2E_RecordedAction_Times"></a>

### Times


```csharp
public int? Times { get; set; }
```


Consecutive identical scrolls folded into one action. Null means one.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L142)

<a id="E2E_RecordedAction_Url_"></a>
<a id="E2E_RecordedAction_Url"></a>

### Url


```csharp
public string? Url { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L136)

<a id="E2E_RecordedAction_Value_"></a>
<a id="E2E_RecordedAction_Value"></a>

### Value


```csharp
public string? Value { get; set; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Cache/StepCache.cs#L132)
