---
title: "Chosen<T>"
description: "What a target question settled (upstream's Chosen): the offered value, with the answer that chose it when one was asked, or why the step cannot go on (Blocked)."
editUrl: false
prev: false
next: false
sidebar:
  label: "Chosen<T>"
  hidden: true
---

<a id="E2E_Decision_Chosen_1"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public sealed class Chosen<T>
```


What a target question settled (upstream's `Chosen`): the offered value, with the answer that chose it when one was asked, or why the
step cannot go on (`Blocked`).

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Picks.cs)

## Properties

<a id="E2E_Decision_Chosen_1_Answer_"></a>
<a id="E2E_Decision_Chosen_1_Answer"></a>

### Answer


```csharp
public Decision? Answer { get; init; }
```


**Value:** [Decision](/api/E2E.Decision/Decision/)

<a id="E2E_Decision_Chosen_1_Blocked_"></a>
<a id="E2E_Decision_Chosen_1_Blocked"></a>

### Blocked


```csharp
public string? Blocked { get; init; }
```


Why the step cannot go on, when it cannot.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Chosen_1_Key_"></a>
<a id="E2E_Decision_Chosen_1_Key"></a>

### Key


```csharp
public string? Key { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Decision_Chosen_1_Value_"></a>
<a id="E2E_Decision_Chosen_1_Value"></a>

### Value


```csharp
public T? Value { get; init; }
```


**Value:** `T`
