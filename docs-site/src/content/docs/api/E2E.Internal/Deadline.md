---
title: "Deadline"
description: "A point in time a wait must end by, as upstream's Deadline."
editUrl: false
prev: false
next: false
sidebar:
  label: "Deadline"
  hidden: true
---

<a id="E2E_Internal_Deadline"></a>

Namespace: [E2E.Internal](/api/E2E.Internal/)


```csharp
public sealed class Deadline
```


A point in time a wait must end by, as upstream's `Deadline`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Internal/Time.cs)

## Constructors

<a id="E2E_Internal_Deadline__ctor_"></a>
<a id="E2E_Internal_Deadline__ctor_System_DateTimeOffset_"></a>

### Deadline(DateTimeOffset)


```csharp
public Deadline(DateTimeOffset endsAt)
```


A point in time a wait must end by, as upstream's `Deadline`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `endsAt` | [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset) |  |

<a id="E2E_Internal_Deadline__ctor_System_TimeSpan_System_TimeProvider_"></a>

### Deadline(TimeSpan, TimeProvider)


```csharp
public Deadline(TimeSpan timeout, TimeProvider clock)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `timeout` | [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan) |  |
| `clock` | [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider) |  |

## Properties

<a id="E2E_Internal_Deadline_EndsAt_"></a>
<a id="E2E_Internal_Deadline_EndsAt"></a>

### EndsAt


```csharp
public DateTimeOffset EndsAt { get; }
```


**Value:** [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

## Methods

<a id="E2E_Internal_Deadline_Expired_"></a>
<a id="E2E_Internal_Deadline_Expired_System_DateTimeOffset_"></a>

### Expired(DateTimeOffset)


```csharp
public bool Expired(DateTimeOffset now)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `now` | [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Internal_Deadline_Min_"></a>
<a id="E2E_Internal_Deadline_Min_E2E_Internal_Deadline_E2E_Internal_Deadline_"></a>

### Min(Deadline, Deadline)


```csharp
public static Deadline Min(Deadline first, Deadline second)
```


The earlier of two deadlines (upstream's `Deadline.min`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `first` | [Deadline](./) |  |
| `second` | [Deadline](./) |  |

**Returns:** [Deadline](./)

<a id="E2E_Internal_Deadline_Remaining_"></a>
<a id="E2E_Internal_Deadline_Remaining_System_DateTimeOffset_"></a>

### Remaining(DateTimeOffset)


```csharp
public TimeSpan Remaining(DateTimeOffset now)
```


Remaining budget, never negative.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `now` | [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset) |  |

**Returns:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)
