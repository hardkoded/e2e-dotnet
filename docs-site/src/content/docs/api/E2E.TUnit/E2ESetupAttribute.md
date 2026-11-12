---
title: "E2ESetupAttribute"
description: "Marks a test of an E2ETest class as a setup test that saves sessions. Upstream's test.setup(title, { sessions }, fn). It goes beside [Test] (TUnit does not l..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2ESetupAttribute"
  hidden: true
---

<a id="E2E_TUnit_E2ESetupAttribute"></a>

Namespace: [E2E.TUnit](/api/E2E.TUnit/)


```csharp
public sealed class E2ESetupAttribute : Attribute, ISessionSetup, ITestDiscoveryEventReceiver, IEventReceiver
```


Marks a test of an [E2ETest](/api/E2E.TUnit/E2ETest/) class as a setup test that saves sessions. Upstream's `test.setup(title, { sessions }, fn)`.
It goes beside `[Test]` (TUnit does not let a library define a test attribute), and [Title](#E2E_TUnit_E2ESetupAttribute_Title) becomes the test's display name. Its body signs in and calls
`SaveSessionAsync` once for each name in [Saves](#E2E_TUnit_E2ESetupAttribute_Saves); a name saved twice, a name not declared, or a declared name left
unsaved fails the test with `SESSION_CONTRACT`. The sessions are saved when the test passes. A test that restores one is marked
[E2ESessionAttribute](/api/E2E/E2ESessionAttribute/).

&lt;p&gt;
A test that restores a session nobody has saved yet runs the setup test that declares it first, once per run, so a dependent can run
alone (upstream's runner schedules the setup too). The dependent runs it on a new instance of the class (parameterless constructor), in a
session of its own and without the class's own `[Before(Test)]` and `[After(Test)]` hooks; exactly one setup test of the assembly must
declare the name, or the restore fails with `SESSION_UNAVAILABLE`, as it does when the setup fails. TUnit then skips that setup test
when its turn comes, and still runs one that no selected test needs, which upstream's runner does not. The setup test gets the highest
execution priority, so TUnit schedules it before the other tests.
&lt;/p&gt;
&lt;p&gt;
TUnit runs tests in parallel, so the setup test and a test that restores its session can start at the same moment, and the setup
then runs twice (once for the dependent, once as its own test). Put `[NotInParallel]` on the class when it must not.
&lt;/p&gt;

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Attribute](https://learn.microsoft.com/dotnet/api/system.attribute)

**Implements:** [ISessionSetup](/api/E2E/ISessionSetup/), `ITestDiscoveryEventReceiver`, `IEventReceiver`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.TUnit/E2ESetupAttribute.cs)

## Constructors

<a id="E2E_TUnit_E2ESetupAttribute__ctor_"></a>
<a id="E2E_TUnit_E2ESetupAttribute__ctor_System_String_"></a>

### E2ESetupAttribute(string)


```csharp
public E2ESetupAttribute(string title)
```


Creates a setup test named &lt;code class="paramref"&gt;title&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `title` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

## Properties

<a id="E2E_TUnit_E2ESetupAttribute_Order_"></a>
<a id="E2E_TUnit_E2ESetupAttribute_Order"></a>

### Order


```csharp
public int Order { get; }
```


Gets the execution order of this event receiver.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32) — The order value as an integer. Default is 0.

Lower values run earlier in the event processing sequence. This allows prioritizing
certain event receivers over others.

<a id="E2E_TUnit_E2ESetupAttribute_Saves_"></a>
<a id="E2E_TUnit_E2ESetupAttribute_Saves"></a>

### Saves


```csharp
public string[] Saves { get; init; }
```


The names of the sessions this test saves. At least one.

**Value:** [string\[\]](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_TUnit_E2ESetupAttribute_Title_"></a>
<a id="E2E_TUnit_E2ESetupAttribute_Title"></a>

### Title


```csharp
public string Title { get; }
```


The test's name.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_TUnit_E2ESetupAttribute_OnTestDiscovered_"></a>
<a id="E2E_TUnit_E2ESetupAttribute_OnTestDiscovered_TUnit_Core_DiscoveredTestContext_"></a>

### OnTestDiscovered(DiscoveredTestContext)


```csharp
public ValueTask OnTestDiscovered(DiscoveredTestContext context)
```


Names the test and schedules it first.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `context` | `DiscoveredTestContext` |  |

**Returns:** [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
