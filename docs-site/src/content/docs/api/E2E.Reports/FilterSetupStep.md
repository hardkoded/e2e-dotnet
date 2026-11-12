---
title: "FilterSetupStep"
description: "Upstream's { kind: 'filter' } step."
editUrl: false
prev: false
next: false
sidebar:
  label: "FilterSetupStep"
  hidden: true
---

<a id="E2E_Reports_FilterSetupStep"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record FilterSetupStep : SetupStep, IEquatable<SetupStep>, IEquatable<FilterSetupStep>
```


Upstream's `{ kind: 'filter' }` step.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [SetupStep](/api/E2E.Reports/SetupStep/)

**Implements:** [IEquatable&lt;SetupStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;FilterSetupStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_FilterSetupStep__ctor_"></a>
<a id="E2E_Reports_FilterSetupStep__ctor_System_String_"></a>

### FilterSetupStep(string)


```csharp
public FilterSetupStep(string Name)
```


Upstream's `{ kind: 'filter' }` step.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Reports_FilterSetupStep_Name_"></a>
<a id="E2E_Reports_FilterSetupStep_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
