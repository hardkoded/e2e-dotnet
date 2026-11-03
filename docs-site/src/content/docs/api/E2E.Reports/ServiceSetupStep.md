---
title: "ServiceSetupStep"
description: "Upstream's { kind: 'service' } step."
editUrl: false
prev: false
next: false
sidebar:
  label: "ServiceSetupStep"
  hidden: true
---

<a id="E2E_Reports_ServiceSetupStep"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
public sealed record ServiceSetupStep : SetupStep, IEquatable<SetupStep>, IEquatable<ServiceSetupStep>
```


Upstream's `{ kind: 'service' }` step.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [SetupStep](/api/E2E.Reports/SetupStep/)

**Implements:** [IEquatable&lt;SetupStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1), [IEquatable&lt;ServiceSetupStep&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Events.cs)

## Constructors

<a id="E2E_Reports_ServiceSetupStep__ctor_"></a>
<a id="E2E_Reports_ServiceSetupStep__ctor_System_String_"></a>

### ServiceSetupStep(string)


```csharp
public ServiceSetupStep(string Label)
```


Upstream's `{ kind: 'service' }` step.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Label` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_Reports_ServiceSetupStep_Label_"></a>
<a id="E2E_Reports_ServiceSetupStep_Label"></a>

### Label


```csharp
public string Label { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
