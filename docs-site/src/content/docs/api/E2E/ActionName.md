---
title: "ActionName"
description: "The name ActionEvent reports a committed action under (upstream's ActionName). Upstream's dismissKeyboard is left out: the port has no such action. A clear r..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ActionName"
  hidden: true
---

<a id="E2E_ActionName"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum ActionName
```


The name [ActionEvent](/api/E2E/ActionEvent/) reports a committed action under (upstream's `ActionName`). Upstream's `dismissKeyboard`
is left out: the port has no such action. A `clear` reads [Type](#E2E_ActionName_Type).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs)

## Fields

<a id="E2E_ActionName_Back"></a>

### Back


```csharp
Back = 16
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_Check"></a>

### Check


```csharp
Check = 10
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_DoubleTap"></a>

### DoubleTap


```csharp
DoubleTap = 1
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_Drag"></a>

### Drag


```csharp
Drag = 12
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_DragAt"></a>

### DragAt


```csharp
DragAt = 19
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_Hover"></a>

### Hover


```csharp
Hover = 4
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_HoverAt"></a>

### HoverAt


```csharp
HoverAt = 18
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_LongPress"></a>

### LongPress


```csharp
LongPress = 2
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_Navigate"></a>

### Navigate


```csharp
Navigate = 15
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_Press"></a>

### Press


```csharp
Press = 8
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_PressKey"></a>

### PressKey


```csharp
PressKey = 21
```


One key sent to whatever held focus (`pressKey`).

**Value:** [ActionName](./)

<a id="E2E_ActionName_Scroll"></a>

### Scroll


```csharp
Scroll = 13
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_ScrollTo"></a>

### ScrollTo


```csharp
ScrollTo = 5
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_ScrollUntil"></a>

### ScrollUntil


```csharp
ScrollUntil = 14
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_SecondaryTap"></a>

### SecondaryTap


```csharp
SecondaryTap = 3
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_Select"></a>

### Select


```csharp
Select = 9
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_Tap"></a>

### Tap


```csharp
Tap = 0
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_TapAt"></a>

### TapAt


```csharp
TapAt = 17
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_Tool"></a>

### Tool


```csharp
Tool = 22
```


A mutating project tool; [ToolName](/api/E2E/ActionEvent/#E2E_ActionEvent_ToolName) names it. Upstream reads `tool:name` in `action`; this value is .NET-only.

**Value:** [ActionName](./)

<a id="E2E_ActionName_Type"></a>

### Type


```csharp
Type = 6
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_TypeSecret"></a>

### TypeSecret


```csharp
TypeSecret = 7
```


**Value:** [ActionName](./)

<a id="E2E_ActionName_TypeText"></a>

### TypeText


```csharp
TypeText = 20
```


Keyboard input to whatever held focus (`typeText`).

**Value:** [ActionName](./)

<a id="E2E_ActionName_Upload"></a>

### Upload


```csharp
Upload = 11
```


**Value:** [ActionName](./)
