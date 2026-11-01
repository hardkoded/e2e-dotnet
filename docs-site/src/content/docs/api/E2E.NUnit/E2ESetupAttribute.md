---
title: "E2ESetupAttribute"
description: "Marks a test of an E2ETest class as a setup test that saves sessions. Upstream's test.setup(title, { sessions }, fn). It is a test: it takes the place of [Te..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ESetupAttribute"
  hidden: true
---

<a id="E2E_NUnit_E2ESetupAttribute"></a>

Namespace: [E2E.NUnit](/api/E2E.NUnit/)


```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class E2ESetupAttribute : TestAttribute, IImplyFixture, ISimpleTestBuilder, IApplyToTest, ISessionSetup
```


Marks a test of an [E2ETest](/api/E2E.NUnit/E2ETest/) class as a setup test that saves sessions. Upstream's `test.setup(title, { sessions }, fn)`.
It is a test: it takes the place of `[Test]`, and [Title](#E2E_NUnit_E2ESetupAttribute_Title) is the test's name. Its body signs in and calls
`SaveSessionAsync` once for each name in [Saves](#E2E_NUnit_E2ESetupAttribute_Saves); a name saved twice, a name not declared, or a declared name left
unsaved fails the test with `SESSION_CONTRACT`. The sessions are saved when the test passes. A test that restores one is marked
[E2ESessionAttribute](/api/E2E/E2ESessionAttribute/).

&lt;p&gt;
NUnit decides the order, not the attribute's dependents: a setup test runs before the tests of its class that are not setup tests, and
before the other fixtures of its namespace unless they set an `[Order]` of their own. Put setup tests in the first fixture,
or order the fixtures. A test that restores a session nobody has saved yet fails with `SESSION_UNAVAILABLE`. Unlike upstream's
runner, NUnit runs a setup test even when no selected test needs its session.
&lt;/p&gt;

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Attribute](https://learn.microsoft.com/dotnet/api/system.attribute) → `NUnitAttribute` → `TestAttribute`

**Implements:** `IImplyFixture`, `ISimpleTestBuilder`, `IApplyToTest`, [ISessionSetup](/api/E2E/ISessionSetup/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ESetupAttribute.cs#L24)

## Constructors

<a id="E2E_NUnit_E2ESetupAttribute__ctor_"></a>
<a id="E2E_NUnit_E2ESetupAttribute__ctor_System_String_"></a>

### E2ESetupAttribute(string)


```csharp
public E2ESetupAttribute(string title)
```


Creates a setup test named &lt;code class="paramref"&gt;title&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `title` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ESetupAttribute.cs#L31)

## Properties

<a id="E2E_NUnit_E2ESetupAttribute_Saves_"></a>
<a id="E2E_NUnit_E2ESetupAttribute_Saves"></a>

### Saves


```csharp
public string[] Saves { get; init; }
```


The names of the sessions this test saves. At least one.

**Value:** [string\[\]](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ESetupAttribute.cs#L40)

<a id="E2E_NUnit_E2ESetupAttribute_Title_"></a>
<a id="E2E_NUnit_E2ESetupAttribute_Title"></a>

### Title


```csharp
public string Title { get; }
```


The test's name.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ESetupAttribute.cs#L37)

## Methods

<a id="E2E_NUnit_E2ESetupAttribute_ApplyToTest_"></a>
<a id="E2E_NUnit_E2ESetupAttribute_ApplyToTest_NUnit_Framework_Internal_Test_"></a>

### ApplyToTest(Test)


```csharp
public void ApplyToTest(Test test)
```


Gives the test an early order unless it has one, and refuses a setup test that saves nothing.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `test` | `Test` |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ESetupAttribute.cs#L54)

<a id="E2E_NUnit_E2ESetupAttribute_BuildFrom_"></a>
<a id="E2E_NUnit_E2ESetupAttribute_BuildFrom_NUnit_Framework_Interfaces_IMethodInfo_NUnit_Framework_Internal_Test_"></a>

### BuildFrom(IMethodInfo, Test?)


```csharp
public TestMethod BuildFrom(IMethodInfo method, Test? suite)
```


Names the test and builds it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `method` | `IMethodInfo` |  |
| `suite` | `Test` |  |

**Returns:** `TestMethod`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/E2ESetupAttribute.cs#L45)
