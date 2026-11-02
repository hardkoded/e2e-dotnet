---
title: "E2ESetupAttribute"
description: "Marks a test of an E2ETest class as a setup test that saves sessions. Upstream's test.setup(title, { sessions }, fn). It is a test: it takes the place of [Fa..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ESetupAttribute"
  hidden: true
---

<a id="E2E_XUnit_V3_E2ESetupAttribute"></a>

Namespace: [E2E.XUnit.V3](/api/E2E.XUnit.V3/)


```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class E2ESetupAttribute : FactAttribute, IFactAttribute, ISessionSetup
```


Marks a test of an [E2ETest](/api/E2E.XUnit.V3/E2ETest/) class as a setup test that saves sessions. Upstream's `test.setup(title, { sessions }, fn)`.
It is a test: it takes the place of `[Fact]`, and [Title](#E2E_XUnit_V3_E2ESetupAttribute_Title) is the test's display name. Its body signs in and calls
`SaveSessionAsync` once for each name in [Saves](#E2E_XUnit_V3_E2ESetupAttribute_Saves); a name saved twice, a name not declared, or a declared name left
unsaved fails the test with `SESSION_CONTRACT`. The sessions are saved when the test passes. A test that restores one is marked
[E2ESessionAttribute](/api/E2E/E2ESessionAttribute/).

&lt;p&gt;
xUnit decides the order, not the attribute's dependents: [E2ETest](/api/E2E.XUnit.V3/E2ETest/) runs the setup tests of a class before its other tests
([SetupTestMethodOrderer](/api/E2E.XUnit.V3/SetupTestMethodOrderer/)), and no earlier than that. Put the setup tests and the tests that use their sessions in one class,
or order the classes yourself: xUnit runs classes in different collections in parallel, so a test in another class may restore a session
before it is saved. A test that restores a session nobody has saved yet fails with `SESSION_UNAVAILABLE`. Unlike upstream's runner,
xUnit runs a setup test even when no selected test needs its session.
&lt;/p&gt;

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Attribute](https://learn.microsoft.com/dotnet/api/system.attribute) → `FactAttribute`

**Implements:** `IFactAttribute`, [ISessionSetup](/api/E2E/ISessionSetup/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/E2ESetupAttribute.cs)

## Constructors

<a id="E2E_XUnit_V3_E2ESetupAttribute__ctor_"></a>
<a id="E2E_XUnit_V3_E2ESetupAttribute__ctor_System_String_System_String_System_Int32_"></a>

### E2ESetupAttribute(string, string?, int)


```csharp
public E2ESetupAttribute(string title, string? sourceFilePath = null, int sourceLineNumber = -1)
```


Creates a setup test named &lt;code class="paramref"&gt;title&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `title` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `sourceFilePath` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `sourceLineNumber` | [int](https://learn.microsoft.com/dotnet/api/system.int32) |  |

## Properties

<a id="E2E_XUnit_V3_E2ESetupAttribute_Saves_"></a>
<a id="E2E_XUnit_V3_E2ESetupAttribute_Saves"></a>

### Saves


```csharp
public string[] Saves { get; init; }
```


The names of the sessions this test saves. At least one.

**Value:** [string\[\]](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_XUnit_V3_E2ESetupAttribute_Title_"></a>
<a id="E2E_XUnit_V3_E2ESetupAttribute_Title"></a>

### Title


```csharp
public string Title { get; }
```


The test's name.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
