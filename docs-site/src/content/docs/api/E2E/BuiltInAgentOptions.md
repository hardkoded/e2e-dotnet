---
title: "BuiltInAgentOptions"
description: "What the built-in agent is built from: an agents entry's system and its tools (upstream's BuiltInAgentOptions)."
editUrl: false
prev: false
next: false
sidebar:
  label: "BuiltInAgentOptions"
  hidden: true
---

<a id="E2E_BuiltInAgentOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class BuiltInAgentOptions
```


What the built-in agent is built from: an agents entry's `system` and its `tools` (upstream's `BuiltInAgentOptions`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/DefaultAgent.cs#L12)

## Properties

<a id="E2E_BuiltInAgentOptions_System_"></a>
<a id="E2E_BuiltInAgentOptions_System"></a>

### System


```csharp
public string? System { get; init; }
```


Appended to the built-in execution rules. Only the act loop reads it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/DefaultAgent.cs#L15)

<a id="E2E_BuiltInAgentOptions_Tools_"></a>
<a id="E2E_BuiltInAgentOptions_Tools"></a>

### Tools


```csharp
public IReadOnlyDictionary<string, AgentTool>? Tools { get; init; }
```


Project tools offered to the model beside the agent's own, by name. A tool is offered only on the platforms it declares.

**Value:** [IReadOnlyDictionary&lt;string, AgentTool&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/DefaultAgent.cs#L20)
