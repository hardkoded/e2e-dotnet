---
title: "E2EReporterAttribute"
description: "Registers a Reporter for the test assembly: [assembly: E2EReporter(typeof(MyReporter))]. .NET-only: upstream registers reporter objects in the reporters fiel..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2EReporterAttribute"
  hidden: true
---

<a id="E2E_Reports_E2EReporterAttribute"></a>

Namespace: [E2E.Reports](/api/E2E.Reports/)


```csharp
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class E2EReporterAttribute : Attribute
```


Registers a [Reporter](/api/E2E.Reports/Reporter/) for the test assembly: `[assembly: E2EReporter(typeof(MyReporter))]`. .NET-only: upstream registers
reporter objects in the `reporters` field of its config module, and the port's JSON config holds no code.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Attribute](https://learn.microsoft.com/dotnet/api/system.attribute)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs)

## Constructors

<a id="E2E_Reports_E2EReporterAttribute__ctor_"></a>
<a id="E2E_Reports_E2EReporterAttribute__ctor_System_Type_"></a>

### E2EReporterAttribute(Type)


```csharp
public E2EReporterAttribute(Type reporterType)
```


Registers a [Reporter](/api/E2E.Reports/Reporter/) for the test assembly: `[assembly: E2EReporter(typeof(MyReporter))]`. .NET-only: upstream registers
reporter objects in the `reporters` field of its config module, and the port's JSON config holds no code.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `reporterType` | [Type](https://learn.microsoft.com/dotnet/api/system.type) | A [Reporter](/api/E2E.Reports/Reporter/) with a public constructor with no parameters. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs)

## Properties

<a id="E2E_Reports_E2EReporterAttribute_ReporterType_"></a>
<a id="E2E_Reports_E2EReporterAttribute_ReporterType"></a>

### ReporterType


```csharp
public Type ReporterType { get; }
```


The reporter's type.

**Value:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs)
