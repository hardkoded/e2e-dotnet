---
title: "SwipePathOptions"
description: "Options for SwipeAsync: a swipe along a path between two viewport points. Upstream's SwipePathOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "SwipePathOptions"
  hidden: true
---

<a id="E2E_SwipePathOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class SwipePathOptions
```


Options for `SwipeAsync`: a swipe along a path between two viewport points. Upstream's `SwipePathOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Locator/Screen.cs)

## Properties

<a id="E2E_SwipePathOptions_Duration_"></a>
<a id="E2E_SwipePathOptions_Duration"></a>

### Duration


```csharp
public TimeSpan? Duration { get; init; }
```


How long the gesture takes: whole milliseconds from 16 through 10000. Without one the engine's default.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_SwipePathOptions_From_"></a>
<a id="E2E_SwipePathOptions_From"></a>

### From


```csharp
public required Point From { get; init; }
```


Where the finger goes down.

**Value:** [Point](/api/E2E.Engine/Point/)

<a id="E2E_SwipePathOptions_To_"></a>
<a id="E2E_SwipePathOptions_To"></a>

### To


```csharp
public required Point To { get; init; }
```


Where it lifts.

**Value:** [Point](/api/E2E.Engine/Point/)
