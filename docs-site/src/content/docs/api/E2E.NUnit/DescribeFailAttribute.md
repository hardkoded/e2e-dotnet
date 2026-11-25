---
title: "DescribeFailAttribute"
description: "Marks every test of the fixture as expected to fail, with the reason expected to fail (upstream's describe.fail(title, fn), the registration form). It is [Pr..."
editUrl: false
prev: false
next: false
sidebar:
  label: "DescribeFailAttribute"
  hidden: true
---

<a id="E2E_NUnit_DescribeFailAttribute"></a>

Namespace: [E2E.NUnit](/api/E2E.NUnit/)


```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class DescribeFailAttribute : PropertyAttribute, IApplyToTest
```


Marks every test of the fixture as expected to fail, with the reason `expected to fail` (upstream's `describe.fail(title, fn)`, the registration
form). It is `[Property("E2E.Fail", "")]` on the fixture: a reason goes in `[Property("E2E.Fail", reason)]` (upstream's `describe(title, { fail: 'reason' }, fn)`),
so this form takes none, as `describe.fail` takes no `fail` option. Like that option, it is refused on, inside, or around a `[Serial]` fixture
with a `COLLECTION_ERROR`. Class-level only, a .NET-only shape: the port has no collection registry, so the host's own discovery finds the attribute.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Attribute](https://learn.microsoft.com/dotnet/api/system.attribute) → `NUnitAttribute` → `PropertyAttribute`

**Implements:** `IApplyToTest`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/DescribeFailAttribute.cs)

## Constructors

<a id="E2E_NUnit_DescribeFailAttribute__ctor_"></a>
<a id="E2E_NUnit_DescribeFailAttribute__ctor"></a>

### DescribeFailAttribute()


```csharp
public DescribeFailAttribute()
```


Marks every test of the fixture as expected to fail, with the reason `expected to fail` (upstream's `describe.fail(title, fn)`, the registration
form). It is `[Property("E2E.Fail", "")]` on the fixture: a reason goes in `[Property("E2E.Fail", reason)]` (upstream's `describe(title, { fail: 'reason' }, fn)`),
so this form takes none, as `describe.fail` takes no `fail` option. Like that option, it is refused on, inside, or around a `[Serial]` fixture
with a `COLLECTION_ERROR`. Class-level only, a .NET-only shape: the port has no collection registry, so the host's own discovery finds the attribute.
