---
title: "ClockCall"
description: "One clock call with its argument resolved to milliseconds, as a replacement context replays it; a call that failed after Playwright logged it counts too (ups..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ClockCall"
  hidden: true
---

<a id="E2E_Engine_ClockCall"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public readonly record struct ClockCall : IEquatable<ClockCall>
```


One clock call with its argument resolved to milliseconds, as a replacement context replays it; a call that failed after Playwright logged it counts too (upstream's `ClockCall`).

**Implements:** [IEquatable&lt;ClockCall&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/WebClock.cs)

## Constructors

<a id="E2E_Engine_ClockCall__ctor_"></a>
<a id="E2E_Engine_ClockCall__ctor_E2E_Engine_ClockMethod_System_Int64_"></a>

### ClockCall(ClockMethod, long)


```csharp
public ClockCall(ClockMethod Method, long Ms = 0)
```


One clock call with its argument resolved to milliseconds, as a replacement context replays it; a call that failed after Playwright logged it counts too (upstream's `ClockCall`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Method` | [ClockMethod](/api/E2E.Engine/ClockMethod/) |  |
| `Ms` | [long](https://learn.microsoft.com/dotnet/api/system.int64) |  |

## Properties

<a id="E2E_Engine_ClockCall_Method_"></a>
<a id="E2E_Engine_ClockCall_Method"></a>

### Method


```csharp
public ClockMethod Method { get; init; }
```


**Value:** [ClockMethod](/api/E2E.Engine/ClockMethod/)

<a id="E2E_Engine_ClockCall_Ms_"></a>
<a id="E2E_Engine_ClockCall_Ms"></a>

### Ms


```csharp
public long Ms { get; init; }
```


**Value:** [long](https://learn.microsoft.com/dotnet/api/system.int64)

<a id="E2E_Engine_ClockCall_Name_"></a>
<a id="E2E_Engine_ClockCall_Name"></a>

### Name


```csharp
public string Name { get; }
```


The method as the API names it, as in `browser.clock.pauseAt`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
