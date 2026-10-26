---
title: "LocatorAction.Fill"
description: "LocatorAction.Fill in E2E.Engine."
editUrl: false
prev: false
next: false
sidebar:
  label: "LocatorAction.Fill"
  hidden: true
---

<a id="E2E_Engine_LocatorAction_Fill"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record LocatorAction.Fill : LocatorAction, IEquatable<LocatorAction>, IEquatable<LocatorAction.Fill>
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [LocatorAction](/api/E2E.Engine/LocatorAction/)

**Implements:** [IEquatable&lt;LocatorAction&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;LocatorAction.Fill&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L20)

## Constructors

<a id="E2E_Engine_LocatorAction_Fill__ctor_"></a>
<a id="E2E_Engine_LocatorAction_Fill__ctor_System_String_System_Boolean_"></a>

### Fill(string, bool)


```csharp
public Fill(string Value, bool Sensitive = false)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Value` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Sensitive` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L20)

## Properties

<a id="E2E_Engine_LocatorAction_Fill_Sensitive_"></a>
<a id="E2E_Engine_LocatorAction_Fill_Sensitive"></a>

### Sensitive


```csharp
public bool Sensitive { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L20)

<a id="E2E_Engine_LocatorAction_Fill_Value_"></a>
<a id="E2E_Engine_LocatorAction_Fill_Value"></a>

### Value


```csharp
public string Value { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/LocatorAction.cs#L20)
