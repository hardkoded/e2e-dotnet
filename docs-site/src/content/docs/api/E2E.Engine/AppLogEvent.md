---
title: "AppLogEvent"
description: "An app log entry the engine reported."
editUrl: false
prev: false
next: false
sidebar:
  label: "AppLogEvent"
  hidden: true
---

<a id="E2E_Engine_AppLogEvent"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record AppLogEvent : AppEvent, IEquatable<AppEvent>, IEquatable<AppLogEvent>
```


An app log entry the engine reported.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [AppEvent](/api/E2E.Engine/AppEvent/)

**Implements:** [IEquatable&lt;AppEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;AppLogEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Surface.cs)

## Constructors

<a id="E2E_Engine_AppLogEvent__ctor_"></a>
<a id="E2E_Engine_AppLogEvent__ctor_E2E_Engine_AppLogEntry_"></a>

### AppLogEvent(AppLogEntry)


```csharp
public AppLogEvent(AppLogEntry Entry)
```


An app log entry the engine reported.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Entry` | [AppLogEntry](/api/E2E.Engine/AppLogEntry/) |  |

## Properties

<a id="E2E_Engine_AppLogEvent_Entry_"></a>
<a id="E2E_Engine_AppLogEvent_Entry"></a>

### Entry


```csharp
public AppLogEntry Entry { get; init; }
```


**Value:** [AppLogEntry](/api/E2E.Engine/AppLogEntry/)
