---
title: "EngineCapabilities"
description: "Capabilities the harness reads off an engine. An engine never calls a model."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineCapabilities"
  hidden: true
---

<a id="E2E_Engine_EngineCapabilities"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
[Flags]
public enum EngineCapabilities
```


Capabilities the harness reads off an engine. An engine never calls a model.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L8)

## Fields

<a id="E2E_Engine_EngineCapabilities_Actions"></a>

### Actions


```csharp
Actions = 2
```


**Value:** [EngineCapabilities](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L13)

<a id="E2E_Engine_EngineCapabilities_History"></a>

### History


```csharp
History = 32
```


[BackAsync(CancellationToken)](/api/E2E.Engine/IEngineSession/#E2E_Engine_IEngineSession_BackAsync_System_Threading_CancellationToken_).

**Value:** [EngineCapabilities](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L21)

<a id="E2E_Engine_EngineCapabilities_Keyboard"></a>

### Keyboard


```csharp
Keyboard = 8
```


**Value:** [EngineCapabilities](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L15)

<a id="E2E_Engine_EngineCapabilities_Location"></a>

### Location


```csharp
Location = 4
```


**Value:** [EngineCapabilities](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L14)

<a id="E2E_Engine_EngineCapabilities_None"></a>

### None


```csharp
None = 0
```


**Value:** [EngineCapabilities](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L11)

<a id="E2E_Engine_EngineCapabilities_Observation"></a>

### Observation


```csharp
Observation = 1
```


**Value:** [EngineCapabilities](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L12)

<a id="E2E_Engine_EngineCapabilities_PinnedApp"></a>

### PinnedApp


```csharp
PinnedApp = 512
```


The engine opens no URL: `CancellationToken)` relaunches the app the target pins. .NET-only name for upstream's
`launchesPinnedApp`, which tests for a session with a `restart` and no `open`; the port's `OpenAsync` is not optional.

**Value:** [EngineCapabilities](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L36)

<a id="E2E_Engine_EngineCapabilities_Screenshot"></a>

### Screenshot


```csharp
Screenshot = 64
```


[ScreenshotAsync(CancellationToken)](/api/E2E.Engine/IEngineSession/#E2E_Engine_IEngineSession_ScreenshotAsync_System_Threading_CancellationToken_), which `toHaveScreenshot` compares.

**Value:** [EngineCapabilities](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L24)

<a id="E2E_Engine_EngineCapabilities_Scroll"></a>

### Scroll


```csharp
Scroll = 16
```


The viewport swipe, [LocatorAction.Swipe](/api/E2E.Engine/LocatorAction.Swipe/), and [LocatorAction.ScrollIntoView](/api/E2E.Engine/LocatorAction.ScrollIntoView/).

**Value:** [EngineCapabilities](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L18)

<a id="E2E_Engine_EngineCapabilities_TapModifiers"></a>

### TapModifiers


```csharp
TapModifiers = 128
```


The `Modifiers` of [LocatorAction.Tap](/api/E2E.Engine/LocatorAction.Tap/) and [LocatorAction.DoubleTap](/api/E2E.Engine/LocatorAction.DoubleTap/): keys held for the click.

**Value:** [EngineCapabilities](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L27)

<a id="E2E_Engine_EngineCapabilities_Video"></a>

### Video


```csharp
Video = 256
```


[StartVideoAsync(CancellationToken)](/api/E2E.Engine/IEngineSession/#E2E_Engine_IEngineSession_StartVideoAsync_System_Threading_CancellationToken_) and [StopVideoAsync(CancellationToken)](/api/E2E.Engine/IEngineSession/#E2E_Engine_IEngineSession_StopVideoAsync_System_Threading_CancellationToken_): the engine can record the attempt as video.

**Value:** [EngineCapabilities](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs#L30)
