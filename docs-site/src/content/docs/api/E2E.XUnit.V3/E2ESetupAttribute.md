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
A test that restores a session nobody has saved yet runs the setup test that declares it first, once per run, so a dependent can run
alone (upstream's runner schedules the setup too). The dependent runs it on a new instance of the class (parameterless constructor), in a
session of its own and without the class's own `InitializeAsync` and `DisposeAsync`; exactly one setup test of the assembly must
declare the name, or the restore fails with `SESSION_UNAVAILABLE`, as it does when the setup fails. xUnit then skips that setup test
when its turn comes, and still runs one that no selected test needs, which upstream's runner does not. [E2ETest](/api/E2E.XUnit.V3/E2ETest/) runs the setup
tests of a class before its other tests ([SetupTestMethodOrderer](/api/E2E.XUnit.V3/SetupTestMethodOrderer/)).
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
