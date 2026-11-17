---
title: "CreateAgentOptions"
description: "The agent's model, budgets, and context, as an agents entry of a config takes them (AgentOptions), plus what only a program sets. Upstream's CreateAgentOptio..."
editUrl: false
prev: false
next: false
sidebar:
  label: "CreateAgentOptions"
  hidden: true
---

<a id="E2E_CreateAgentOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class CreateAgentOptions : AgentOptions
```


The agent's model, budgets, and context, as an `agents` entry of a config takes them ([AgentOptions](/api/E2E/AgentOptions/)), plus what only a program sets. Upstream's `CreateAgentOptions` (`core/agent.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object) → [AgentOptions](/api/E2E/AgentOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/CoreAgent.cs)

## Properties

<a id="E2E_CreateAgentOptions_Cache_"></a>
<a id="E2E_CreateAgentOptions_Cache"></a>

### Cache


```csharp
public AgentCacheOptions? Cache { get; init; }
```


Replays recorded `ActAsync` flows; every `ActAsync` then names its recording with [CacheKey](/api/E2E/CachingActOptions/#E2E_CachingActOptions_CacheKey). Unset, every `ActAsync` calls the model.

**Value:** [AgentCacheOptions](/api/E2E/AgentCacheOptions/)
