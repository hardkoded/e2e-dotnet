---
title: "ClockTime"
description: "A point in time for Browser.Clock (upstream's ClockTime): epoch milliseconds, a string a Date parses, or a DateTimeOffset. A fraction of a millisecond is dro..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ClockTime"
  hidden: true
---

<a id="E2E_Engine_ClockTime"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public readonly struct ClockTime
```


A point in time for `Browser.Clock` (upstream's `ClockTime`): epoch milliseconds, a string a
`Date` parses, or a [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset). A fraction of a millisecond is dropped.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebClock.cs)

## Operators

<a id="E2E_Engine_ClockTime_op_Implicit_"></a>
<a id="E2E_Engine_ClockTime_op_Implicit_System_DateTime__E2E_Engine_ClockTime"></a>

### implicit operator ClockTime(DateTime)


```csharp
public static implicit operator ClockTime(DateTime date)
```


An instant; a [Unspecified](https://learn.microsoft.com/dotnet/api/system.datetimekind#system-datetimekind-unspecified) value is local time.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `date` | [DateTime](https://learn.microsoft.com/dotnet/api/system.datetime) |  |

**Returns:** [ClockTime](./)

<a id="E2E_Engine_ClockTime_op_Implicit_System_DateTimeOffset__E2E_Engine_ClockTime"></a>

### implicit operator ClockTime(DateTimeOffset)


```csharp
public static implicit operator ClockTime(DateTimeOffset date)
```


An instant.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `date` | [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset) |  |

**Returns:** [ClockTime](./)

<a id="E2E_Engine_ClockTime_op_Implicit_System_Double__E2E_Engine_ClockTime"></a>

### implicit operator ClockTime(double)


```csharp
public static implicit operator ClockTime(double milliseconds)
```


Epoch milliseconds.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `milliseconds` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

**Returns:** [ClockTime](./)

<a id="E2E_Engine_ClockTime_op_Implicit_System_Int64__E2E_Engine_ClockTime"></a>

### implicit operator ClockTime(long)


```csharp
public static implicit operator ClockTime(long milliseconds)
```


Epoch milliseconds.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `milliseconds` | [long](https://learn.microsoft.com/dotnet/api/system.int64) |  |

**Returns:** [ClockTime](./)

<a id="E2E_Engine_ClockTime_op_Implicit_System_String__E2E_Engine_ClockTime"></a>

### implicit operator ClockTime(string)


```csharp
public static implicit operator ClockTime(string text)
```


A date string, read as a JavaScript `Date` reads it: a date alone is UTC, a time without an offset is local.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ClockTime](./)
