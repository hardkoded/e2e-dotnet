---
title: "AgentAttribute"
description: "Pins the agent that the calls of a test, or of every test in a class, use when they name none. Upstream's agent option of test and describe. The closest pin..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AgentAttribute"
  hidden: true
---

<a id="E2E_AgentAttribute"></a>

Namespace: [E2E](/api/E2E/)


```csharp
[AttributeUsage(AttributeTargets.Class|AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AgentAttribute : Attribute
```


Pins the agent that the calls of a test, or of every test in a class, use when they name none. Upstream's
`agent` option of `test` and `describe`. The closest pin wins: a call's [Agent](/api/E2E/ActOptions/#E2E_ActOptions_Agent),
then the method, then the class, then an enclosing or base class, then the `E2E_AGENT` environment variable. Base and enclosing classes count although
the attribute is not inherited: `TestAgentOptions` reads each layer itself.
An unknown name fails the test with `INVALID_ARGUMENT`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Attribute](https://learn.microsoft.com/dotnet/api/system.attribute)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/AgentAttribute.cs#L14)

## Constructors

<a id="E2E_AgentAttribute__ctor_"></a>
<a id="E2E_AgentAttribute__ctor_System_String_"></a>

### AgentAttribute(string)


```csharp
public AgentAttribute(string name)
```


Pins the agent that the calls of a test, or of every test in a class, use when they name none. Upstream's
`agent` option of `test` and `describe`. The closest pin wins: a call's [Agent](/api/E2E/ActOptions/#E2E_ActOptions_Agent),
then the method, then the class, then an enclosing or base class, then the `E2E_AGENT` environment variable. Base and enclosing classes count although
the attribute is not inherited: `TestAgentOptions` reads each layer itself.
An unknown name fails the test with `INVALID_ARGUMENT`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/AgentAttribute.cs#L14)

## Properties

<a id="E2E_AgentAttribute_Name_"></a>
<a id="E2E_AgentAttribute_Name"></a>

### Name


```csharp
public string Name { get; }
```


The name of an `agents.<name>` entry.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/AgentAttribute.cs#L18)
