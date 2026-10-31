---
title: "LocatorAction.Select"
description: "LocatorAction.Select in E2E.Engine."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.Select"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_Select"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.Select : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.Select>
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.Select&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L39)

## Constructors

<a id="E2E_Engine_LocatorAction_Select__ctor_"></a>
<a id="E2E_Engine_LocatorAction_Select__ctor_System_String_"></a>

### Select(string)


```csharp
public Select(string Value)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L39)

## Properties

<a id="E2E_Engine_LocatorAction_Select_Value_"></a>
<a id="E2E_Engine_LocatorAction_Select_Value"></a>

### Value


```csharp
public string Value { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L39)
