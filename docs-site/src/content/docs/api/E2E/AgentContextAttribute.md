---
title: "AgentContextAttribute"
description: "Appends facts only this test knows to the context of every agent, after the agent's own context. Upstream's agentContext option of test and describe. Layers..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AgentContextAttribute"
  hidden: true
---

<a id="E2E_AgentContextAttribute"></a>

Namespace: [E2E](/api/E2E/)


```csharp
[AttributeUsage(AttributeTargets.Class|AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AgentContextAttribute : Attribute
```


Appends facts only this test knows to the context of every agent, after the agent's own `context`. Upstream's
`agentContext` option of `test` and `describe`. Layers join outer to inner, one per line. Keep instructions
out of it: it reaches the judges too.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [Attribute](https://learn.microsoft.com/dotnet/api/system.attribute)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/AgentAttribute.cs#L26)

## Constructors

<a id="E2E_AgentContextAttribute__ctor_"></a>
<a id="E2E_AgentContextAttribute__ctor_System_String_"></a>

### AgentContextAttribute(string)


```csharp
public AgentContextAttribute(string text)
```


Appends facts only this test knows to the context of every agent, after the agent's own `context`. Upstream's
`agentContext` option of `test` and `describe`. Layers join outer to inner, one per line. Keep instructions
out of it: it reaches the judges too.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `text` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/AgentAttribute.cs#L26)

## Properties

<a id="E2E_AgentContextAttribute_Text_"></a>
<a id="E2E_AgentContextAttribute_Text"></a>

### Text


```csharp
public string Text { get; }
```


The text to append.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/AgentAttribute.cs#L30)
