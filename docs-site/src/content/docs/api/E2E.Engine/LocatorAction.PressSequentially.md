---
title: "LocatorAction.PressSequentially"
description: "Focuses the node and types <code class=\"paramref\">Text</code> one character at a time."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.PressSequentially"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_PressSequentially"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.PressSequentially : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.PressSequentially>
```


Focuses the node and types &lt;code class="paramref"&gt;Text&lt;/code&gt; one character at a time.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.PressSequentially&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L37)

## Constructors

<a id="E2E_Engine_LocatorAction_PressSequentially__ctor_"></a>
<a id="E2E_Engine_LocatorAction_PressSequentially__ctor_System_String_System_Nullable_System_TimeSpan__"></a>

### PressSequentially(string, TimeSpan?)


```csharp
public PressSequentially(string Text, TimeSpan? Delay = null)
```


Focuses the node and types &lt;code class="paramref"&gt;Text&lt;/code&gt; one character at a time.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Delay` | [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L37)

## Properties

<a id="E2E_Engine_LocatorAction_PressSequentially_Delay_"></a>
<a id="E2E_Engine_LocatorAction_PressSequentially_Delay"></a>

### Delay


```csharp
public TimeSpan? Delay { get; init; }
```


**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L37)

<a id="E2E_Engine_LocatorAction_PressSequentially_Text_"></a>
<a id="E2E_Engine_LocatorAction_PressSequentially_Text"></a>

### Text


```csharp
public string Text { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L37)
