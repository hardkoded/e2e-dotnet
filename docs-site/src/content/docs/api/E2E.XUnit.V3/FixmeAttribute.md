---
title: "FixmeAttribute"
description: "Registers the test as fixme (upstream's test.fixme(title, fn), the registration form). The test is skipped with the cause fixme and the reason fixme, or fixm..."
editUrl: false
prev: false
next: false
sidebar:
  label: "FixmeAttribute"
  hidden: true
---

<a id="E2E_XUnit_V3_FixmeAttribute"></a>

Namespace: [E2E.XUnit.V3](/api/E2E.XUnit.V3/)


```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FixmeAttribute : Attribute
```


Registers the test as fixme (upstream's `test.fixme(title, fn)`, the registration form). The test is skipped with the cause
`fixme` and the reason `fixme`, or `fixme: <reason>` for a reason that is not blank, and it is reported as
such, not as an explicit skip. It starts its session and skips before its body, as `Fixme()` does from a set-up. A `NO_TESTS`
message counts it as `marked fixme`. Method-level only, a .NET-only shape: the port has no collection registry, so the host's own
discovery finds the attribute.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Attribute](https://learn.microsoft.com/dotnet/api/system.attribute)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/FixmeAttribute.cs)

## Constructors

<a id="E2E_XUnit_V3_FixmeAttribute__ctor_"></a>
<a id="E2E_XUnit_V3_FixmeAttribute__ctor_System_String_"></a>

### FixmeAttribute(string?)


```csharp
public FixmeAttribute(string? reason = null)
```


Registers the test as fixme (upstream's `test.fixme(title, fn)`, the registration form). The test is skipped with the cause
`fixme` and the reason `fixme`, or `fixme: <reason>` for a reason that is not blank, and it is reported as
such, not as an explicit skip. It starts its session and skips before its body, as `Fixme()` does from a set-up. A `NO_TESTS`
message counts it as `marked fixme`. Method-level only, a .NET-only shape: the port has no collection registry, so the host's own
discovery finds the attribute.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `reason` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_XUnit_V3_FixmeAttribute_Reason_"></a>
<a id="E2E_XUnit_V3_FixmeAttribute_Reason"></a>

### Reason


```csharp
public string? Reason { get; }
```


The reason the test is marked fixme; a blank or missing reason reports just `fixme`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
