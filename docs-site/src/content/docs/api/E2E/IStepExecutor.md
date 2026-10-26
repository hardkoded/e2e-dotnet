---
title: "IStepExecutor"
description: "Upstream's StepExecutor (agent/executor.ts): the part of an act step that thinks. The runtime owns the step's cache, budget, deadline, and verdict; an execut..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IStepExecutor"
  hidden: true
---

<a id="E2E_IStepExecutor"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public interface IStepExecutor
```


Upstream's `StepExecutor` (`agent/executor.ts`): the part of an act step that thinks. The runtime owns
the step's cache, budget, deadline, and verdict; an executor reads the screen, acts through the context, and
returns a verdict. Swapping executors changes the thinking, never the safety, the budgets, or the verdict grammar.

&lt;p&gt;
An executor is trusted project code, in the same trust domain as the test project that builds it: it may hold its
own model and credentials. What the runtime enforces against it is accounting, not secrecy: the budgets, the
deadline, the recording, and the verdict grammar hold whoever thinks.
&lt;/p&gt;

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L20)
