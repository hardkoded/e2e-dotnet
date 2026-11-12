---
title: "NoticeEvent"
description: "Upstream's notice fact: one line of run-level progress outside any test."
editUrl: false
prev: false
next: false
sidebar:
  label: "NoticeEvent"
  hidden: true
---

<a id="E2E_Reports_NoticeEvent"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record NoticeEvent : RunEventFact, IEquatable<RunEventFact>, IEquatable<NoticeEvent>
```


Upstream's `notice` fact: one line of run-level progress outside any test.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [RunEventFact](/api/E2E.Reports/RunEventFact/)

**Implements:** [IEquatable&lt;RunEventFact&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;NoticeEvent&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_NoticeEvent__ctor_"></a>
<a id="E2E_Reports_NoticeEvent__ctor_System_String_System_String_"></a>

### NoticeEvent(string, string)


```csharp
public NoticeEvent(string Target, string Message)
```


Upstream's `notice` fact: one line of run-level progress outside any test.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Target` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Message` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Reports_NoticeEvent_Message_"></a>
<a id="E2E_Reports_NoticeEvent_Message"></a>

### Message


```csharp
public string Message { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_NoticeEvent_Target_"></a>
<a id="E2E_Reports_NoticeEvent_Target"></a>

### Target


```csharp
public string Target { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
