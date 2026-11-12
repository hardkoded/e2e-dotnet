---
title: "EngineKeyboardSpec"
description: "Upstream's keyboard hooks of an engine (engine/index.ts): typing, pressing, and dismissing."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineKeyboardSpec"
  hidden: true
---

<a id="E2E_Engine_EngineKeyboardSpec"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineKeyboardSpec : IEquatable<EngineKeyboardSpec>
```


Upstream's `keyboard` hooks of an engine (`engine/index.ts`): typing, pressing, and dismissing.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineKeyboardSpec&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/Manifest.cs)

## Properties

<a id="E2E_Engine_EngineKeyboardSpec_Dismiss_"></a>
<a id="E2E_Engine_EngineKeyboardSpec_Dismiss"></a>

### Dismiss


```csharp
public Func<OperationContext, Task>? Dismiss { get; init; }
```


Dismisses the on-screen keyboard.

**Value:** [Func&lt;OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Engine_EngineKeyboardSpec_Press_"></a>
<a id="E2E_Engine_EngineKeyboardSpec_Press"></a>

### Press


```csharp
public Func<string, OperationContext, Task>? Press { get; init; }
```


Presses one key, in the contract's key grammar.

**Value:** [Func&lt;string, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

<a id="E2E_Engine_EngineKeyboardSpec_Type_"></a>
<a id="E2E_Engine_EngineKeyboardSpec_Type"></a>

### Type


```csharp
public Func<string, bool, OperationContext, Task>? Type { get; init; }
```


Types text into the focused field; `replace` clears the field first.

**Value:** [Func&lt;string, bool, OperationContext, Task&gt;](https://learn.microsoft.com/dotnet/api/system.func-4)
