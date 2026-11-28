---
title: "OnlyAttribute"
description: "Focuses the test or the fixture (upstream's test.only and describe.only). When any selected test is focused, the run executes the focused tests, those of a f..."
editUrl: false
prev: false
next: false
sidebar:
  label: "OnlyAttribute"
  hidden: true
---

<a id="E2E_TUnit_OnlyAttribute"></a>

Namespace: [E2E.TUnit](/api/E2E.TUnit/)


```csharp
[AttributeUsage(AttributeTargets.Class|AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class OnlyAttribute : Attribute
```


Focuses the test or the fixture (upstream's `test.only` and `describe.only`). When any selected test is focused, the run executes the
focused tests, those of a focused fixture and of the fixtures nested in it included, and reports every other test as skipped with the cause
`filtered`. A focused test in a CI run is refused with `ONLY_IN_CI`, as upstream refuses `.only` there.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Attribute](https://learn.microsoft.com/dotnet/api/system.attribute)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.TUnit/OnlyAttribute.cs)
