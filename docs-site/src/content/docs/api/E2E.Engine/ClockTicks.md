---
title: "ClockTicks"
description: "A span of time for Browser.Clock (upstream's ClockTicks): milliseconds, or an 'ss', 'mm:ss', or 'hh:mm:ss' string. A fraction of a millisecond is dropped."
editUrl: false
prev: false
next: false
sidebar:
  label: "ClockTicks"
  hidden: true
---

<a id="E2E_Engine_ClockTicks"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public readonly struct ClockTicks
```


A span of time for `Browser.Clock` (upstream's `ClockTicks`): milliseconds, or an `'ss'`, `'mm:ss'`, or
`'hh:mm:ss'` string. A fraction of a millisecond is dropped.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebClock.cs)

## Operators

<a id="E2E_Engine_ClockTicks_op_Implicit_"></a>
<a id="E2E_Engine_ClockTicks_op_Implicit_System_Double__E2E_Engine_ClockTicks"></a>

### implicit operator ClockTicks(double)


```csharp
public static implicit operator ClockTicks(double milliseconds)
```


Milliseconds.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `milliseconds` | [double](https://learn.microsoft.com/dotnet/api/system.double) |  |

**Returns:** [ClockTicks](./)

<a id="E2E_Engine_ClockTicks_op_Implicit_System_Int64__E2E_Engine_ClockTicks"></a>

### implicit operator ClockTicks(long)


```csharp
public static implicit operator ClockTicks(long milliseconds)
```


Milliseconds.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `milliseconds` | [long](https://learn.microsoft.com/dotnet/api/system.int64) |  |

**Returns:** [ClockTicks](./)

<a id="E2E_Engine_ClockTicks_op_Implicit_System_String__E2E_Engine_ClockTicks"></a>

### implicit operator ClockTicks(string)


```csharp
public static implicit operator ClockTicks(string text)
```


An `'ss'`, `'mm:ss'`, or `'hh:mm:ss'` string with every field under 60.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [ClockTicks](./)
