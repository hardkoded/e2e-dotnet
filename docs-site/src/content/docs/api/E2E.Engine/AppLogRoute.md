---
title: "AppLogRoute"
description: "Upstream's AppLogRoute (engine/session.ts): where the engine's app log goes. The engine reports from startAttempt on, before any step records; what arrives b..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AppLogRoute"
  hidden: true
---

<a id="E2E_Engine_AppLogRoute"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed class AppLogRoute
```


Upstream's `AppLogRoute` (`engine/session.ts`): where the engine's app log goes. The engine reports from `startAttempt` on,
before any step records; what arrives before a sink is set waits, up to a bound per kind, and each kind has its own quota so chatter never
crowds out an error.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Surface.cs)

## Fields

<a id="E2E_Engine_AppLogRoute_MaxErrorsAndWarnings"></a>

### MaxErrorsAndWarnings


```csharp
public const int MaxErrorsAndWarnings = 200
```


Errors and warnings held while no sink is set.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_AppLogRoute_MaxInfo"></a>

### MaxInfo


```csharp
public const int MaxInfo = 100
```


Info entries held while no sink is set.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_Engine_AppLogRoute_MaxNavigations"></a>

### MaxNavigations


```csharp
public const int MaxNavigations = 50
```


Navigations held while no sink is set.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

<a id="E2E_Engine_AppLogRoute_Navigated_"></a>
<a id="E2E_Engine_AppLogRoute_Navigated_System_String_"></a>

### Navigated(string)


```csharp
public void Navigated(string line)
```


Takes one line saying where the app went; a blank one is dropped.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `line` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

<a id="E2E_Engine_AppLogRoute_Push_"></a>
<a id="E2E_Engine_AppLogRoute_Push_E2E_Engine_AppLogEntry_"></a>

### Push(AppLogEntry)


```csharp
public void Push(AppLogEntry entry)
```


Takes one entry from the engine, checked: a malformed one is dropped.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `entry` | [AppLogEntry](/api/E2E.Engine/AppLogEntry/) |  |

<a id="E2E_Engine_AppLogRoute_Route_"></a>
<a id="E2E_Engine_AppLogRoute_Route_System_Action_E2E_Engine_AppEvent_System_DateTimeOffset__"></a>

### `Route(Action<AppEvent, DateTimeOffset>?)`


```csharp
public void Route(Action<AppEvent, DateTimeOffset>? sink)
```


Sends everything from now on, and everything still waiting, to &lt;code class="paramref"&gt;sink&lt;/code&gt;; null holds it again.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `sink` | [Action&lt;AppEvent, DateTimeOffset&gt;](https://learn.microsoft.com/dotnet/api/system.action-2) |  |
