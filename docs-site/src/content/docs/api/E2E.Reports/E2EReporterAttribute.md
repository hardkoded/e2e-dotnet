---
title: "E2EReporterAttribute"
description: "Registers a Reporter for the test assembly: `[assembly: E2EReporter(typeof(MyReporter))]`. .NET-only: upstream registers reporter objects in the `reporters` field of its config module, and the port's JSON config holds no code."
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


Registers a Reporter for the test assembly: `[assembly: E2EReporter(typeof(MyReporter))]`. .NET-only: upstream registers reporter objects in the `reporters` field of its config module, and the port's JSON config holds no code.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Report/Reporter.cs#L45)

## Constructors

### E2EReporterAttribute(Type)


```csharp
public E2EReporterAttribute(Type reporterType)
```


`reporterType` is a Reporter with a public constructor with no parameters.

## Properties

### ReporterType


```csharp
public Type ReporterType { get; }
```
