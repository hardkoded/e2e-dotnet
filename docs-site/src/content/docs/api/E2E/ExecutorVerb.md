---
title: "ExecutorVerb"
description: "One verb of IExecutorActions. The port has the verbs its engines can perform."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorVerb"
  hidden: true
---

<a id="E2E_ExecutorVerb"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum ExecutorVerb
```


One verb of [IExecutorActions](/api/E2E/IExecutorActions/). The port has the verbs its engines can perform.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Fields

<a id="E2E_ExecutorVerb_Back"></a>

### Back


```csharp
Back = 16
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Check"></a>

### Check


```csharp
Check = 11
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Clear"></a>

### Clear


```csharp
Clear = 8
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_CloseTab"></a>

### CloseTab


```csharp
CloseTab = 26
```


Upstream's `closeTab`: `CloseTabAsync`, with an engine that declares [CloseTab](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_CloseTab).

**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_DismissKeyboard"></a>

### DismissKeyboard


```csharp
DismissKeyboard = 23
```


Upstream's `dismissKeyboard`: [DismissKeyboardAsync(CancellationToken)](/api/E2E/IExecutorActions/#E2E_IExecutorActions_DismissKeyboardAsync_System_Threading_CancellationToken_), with an engine whose [CanDismissKeyboard](/api/E2E.Engine/IEngineSession/#E2E_Engine_IEngineSession_CanDismissKeyboard) is true.

**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_DoubleTap"></a>

### DoubleTap


```csharp
DoubleTap = 1
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Drag"></a>

### Drag


```csharp
Drag = 5
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_DragAt"></a>

### DragAt


```csharp
DragAt = 20
```


Upstream's `dragAt`: `DragAtAsync`, with the engine's `dragTo` pointer kind.

**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Hover"></a>

### Hover


```csharp
Hover = 4
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_HoverAt"></a>

### HoverAt


```csharp
HoverAt = 19
```


Upstream's `hoverAt`: `HoverAtAsync`, with the engine's `hover` pointer kind.

**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_LongPress"></a>

### LongPress


```csharp
LongPress = 2
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Navigate"></a>

### Navigate


```csharp
Navigate = 15
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Press"></a>

### Press


```csharp
Press = 9
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_PressKey"></a>

### PressKey


```csharp
PressKey = 22
```


Upstream's `pressKey`: `PressKeyAsync`, with an engine that declares [Keyboard](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_Keyboard).

**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Resize"></a>

### Resize


```csharp
Resize = 24
```


Upstream's `resize`: `ResizeAsync`, with an engine that declares [Resize](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_Resize).

**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Scroll"></a>

### Scroll


```csharp
Scroll = 14
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_ScrollTo"></a>

### ScrollTo


```csharp
ScrollTo = 12
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_ScrollUntil"></a>

### ScrollUntil


```csharp
ScrollUntil = 13
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_SecondaryTap"></a>

### SecondaryTap


```csharp
SecondaryTap = 3
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Select"></a>

### Select


```csharp
Select = 10
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_SwitchTab"></a>

### SwitchTab


```csharp
SwitchTab = 25
```


Upstream's `switchTab`: `SwitchTabAsync`, with an engine that declares [SwitchTab](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_SwitchTab).

**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Tap"></a>

### Tap


```csharp
Tap = 0
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_TapAt"></a>

### TapAt


```csharp
TapAt = 18
```


Upstream's `tapAt`: `TapAtAsync`, with the engine's `tap` pointer kind.

**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Type"></a>

### Type


```csharp
Type = 6
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_TypeSecret"></a>

### TypeSecret


```csharp
TypeSecret = 7
```


**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_TypeText"></a>

### TypeText


```csharp
TypeText = 21
```


Upstream's `typeText`: `TypeTextAsync`, with an engine that declares [Keyboard](/api/E2E.Engine/EngineCapabilities/#E2E_Engine_EngineCapabilities_Keyboard).

**Value:** [ExecutorVerb](./)

<a id="E2E_ExecutorVerb_Upload"></a>

### Upload


```csharp
Upload = 17
```


**Value:** [ExecutorVerb](./)
