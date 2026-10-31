---
title: "IExecutorActions"
description: "Upstream's ExecutorActions, with the verbs the port's engines support. Every verb bottoms out in the runtime, which enforces the deadline and the action budg..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IExecutorActions"
  hidden: true
---

<a id="E2E_IExecutorActions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public interface IExecutorActions
```


Upstream's `ExecutorActions`, with the verbs the port's engines support. Every verb bottoms out in the runtime, which
enforces the deadline and the action budget and records the action for the step's cache entry. A ref is only valid against
the newest observation: a stale one fails the action rather than acting on the wrong node. Actions and observations run
in call order. An engine failure is thrown as an [E2EException](/api/E2E/E2EException/), and a hard stop of the step as an
[AgentException](/api/E2E/AgentException/) with a runtime code. `CancellationToken)` is a verb the port's grammar has and
upstream's does not (upstream clears with `type`).

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L378)
