---
title: "PrepareSetupStep"
description: "Upstream's { kind: 'prepare' } step."
editUrl: false
prev: false
next: false
sidebar:
  label: "PrepareSetupStep"
  hidden: true
---

<a id="E2E_Reports_PrepareSetupStep"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record PrepareSetupStep : SetupStep, IEquatable<SetupStep>, IEquatable<PrepareSetupStep>
```


Upstream's `{ kind: 'prepare' }` step.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [SetupStep](/api/E2E.Reports/SetupStep/)

**Implements:** [IEquatable&lt;SetupStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;PrepareSetupStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_PrepareSetupStep__ctor_"></a>
<a id="E2E_Reports_PrepareSetupStep__ctor_System_String_System_String_"></a>

### PrepareSetupStep(string, string)


```csharp
public PrepareSetupStep(string Target, string Engine)
```


Upstream's `{ kind: 'prepare' }` step.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Target` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `Engine` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Reports_PrepareSetupStep_Engine_"></a>
<a id="E2E_Reports_PrepareSetupStep_Engine"></a>

### Engine


```csharp
public string Engine { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Reports_PrepareSetupStep_Target_"></a>
<a id="E2E_Reports_PrepareSetupStep_Target"></a>

### Target


```csharp
public string Target { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
