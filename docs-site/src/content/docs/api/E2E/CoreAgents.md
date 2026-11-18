---
title: "CoreAgents"
description: "createAgent from @e2e-dev/core/agent: the agent over one session, configured in code. Upstream's core/agent.ts."
editUrl: false
prev: false
next: false
sidebar:
  label: "CoreAgents"
  hidden: true
---

<a id="E2E_CoreAgents"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class CoreAgents
```


`createAgent` from `@e2e-dev/core/agent`: the agent over one session, configured in code. Upstream's `core/agent.ts`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/CoreAgent.cs)

## Methods

<a id="E2E_CoreAgents_CreateAgent_"></a>
<a id="E2E_CoreAgents_CreateAgent_E2E_Session_E2E_CreateAgentOptions_"></a>

### CreateAgent(Session, CreateAgentOptions)


```csharp
public static CoreAgent CreateAgent(Session session, CreateAgentOptions options)
```


Creates an agent over &lt;code class="paramref"&gt;session&lt;/code&gt;. With [Cache](/api/E2E/CreateAgentOptions/#E2E_CreateAgentOptions_Cache), its `ActAsync` replays flows,
each named by its [CacheKey](/api/E2E/CachingActOptions/#E2E_CachingActOptions_CacheKey), and records them when the session closes. A missing model fails the first call that
needs one as `MODEL_UNAVAILABLE`, never the creation.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `session` | [Session](/api/E2E/Session/) |  |
| `options` | [CreateAgentOptions](/api/E2E/CreateAgentOptions/) |  |

**Returns:** [CoreAgent](/api/E2E/CoreAgent/)
