---
title: "E2ESessionAttribute"
description: "Starts a test, or every test of a class, from a session a setup test saved. Upstream's session option of test and describe. The session restores before the t..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ESessionAttribute"
  hidden: true
---

<a id="E2E_E2ESessionAttribute"></a>

Namespace: [E2E](/api/E2E/)


```csharp
[AttributeUsage(AttributeTargets.Class|AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class E2ESessionAttribute : Attribute
```


Starts a test, or every test of a class, from a session a setup test saved. Upstream's `session` option of `test` and
`describe`. The session restores before the test body runs; a restore leaves no page open, so the test opens one first. The
setup test must run before the test (in NUnit, give it an earlier `[Order]`); a session no setup produced fails the test with
`SESSION_UNAVAILABLE`, and an engine that cannot restore state with `UNSUPPORTED_CAPABILITY`. The closest attribute wins:
the method, then the class, then a base class. .NET-only name, as the port has no `test()` options object.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Attribute](https://learn.microsoft.com/dotnet/api/system.attribute)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/SessionAttributes.cs#L16)

## Constructors

<a id="E2E_E2ESessionAttribute__ctor_"></a>
<a id="E2E_E2ESessionAttribute__ctor_System_String_"></a>

### E2ESessionAttribute(string)


```csharp
public E2ESessionAttribute(string name)
```


Starts a test, or every test of a class, from a session a setup test saved. Upstream's `session` option of `test` and
`describe`. The session restores before the test body runs; a restore leaves no page open, so the test opens one first. The
setup test must run before the test (in NUnit, give it an earlier `[Order]`); a session no setup produced fails the test with
`SESSION_UNAVAILABLE`, and an engine that cannot restore state with `UNSUPPORTED_CAPABILITY`. The closest attribute wins:
the method, then the class, then a base class. .NET-only name, as the port has no `test()` options object.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/SessionAttributes.cs#L16)

## Properties

<a id="E2E_E2ESessionAttribute_Name_"></a>
<a id="E2E_E2ESessionAttribute_Name"></a>

### Name


```csharp
public string Name { get; }
```


The name a setup test saved the session under.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/SessionAttributes.cs#L20)
