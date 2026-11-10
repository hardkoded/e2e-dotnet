---
title: "AppNavigationEvent"
description: "A line saying where the app went (upstream's navigated)."
editUrl: false
prev: false
next: false
sidebar:
  label: "AppNavigationEvent"
  hidden: true
---

<a id="E2E_Engine_AppNavigationEvent"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record AppNavigationEvent : AppEvent, IEquatable<AppEvent>, IEquatable<AppNavigationEvent>
```


A line saying where the app went (upstream's `navigated`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [AppEvent](/api/E2E.Engine/AppEvent/)

**Implements:** [IEquatable&lt;AppEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;AppNavigationEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Surface.cs)

## Constructors

<a id="E2E_Engine_AppNavigationEvent__ctor_"></a>
<a id="E2E_Engine_AppNavigationEvent__ctor_System_String_"></a>

### AppNavigationEvent(string)


```csharp
public AppNavigationEvent(string Line)
```


A line saying where the app went (upstream's `navigated`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Line` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Engine_AppNavigationEvent_Line_"></a>
<a id="E2E_Engine_AppNavigationEvent_Line"></a>

### Line


```csharp
public string Line { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
