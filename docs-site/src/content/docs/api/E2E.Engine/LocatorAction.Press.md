---
title: "LocatorAction.Press"
description: "LocatorAction.Press in E2E.Engine."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.Press"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_Press"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.Press : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.Press>
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.Press&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L22)

## Constructors

<a id="E2E_Engine_LocatorAction_Press__ctor_"></a>
<a id="E2E_Engine_LocatorAction_Press__ctor_System_String_"></a>

### Press(string)


```csharp
public Press(string Key)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Key` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L22)

## Properties

<a id="E2E_Engine_LocatorAction_Press_Key_"></a>
<a id="E2E_Engine_LocatorAction_Press_Key"></a>

### Key


```csharp
public string Key { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L22)
