---
title: "OperationOrigin"
description: "Who acts: a test's own deterministic step, or the agent acting on a screen it observed. A deterministic step verifies its outcome with expect, so an engine m..."
editUrl: false
prev: false
next: false
sidebar:
  label: "OperationOrigin"
  hidden: true
---

<a id="E2E_Engine_OperationOrigin"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public enum OperationOrigin
```


Who acts: a test's own deterministic step, or the agent acting on a screen it observed. A deterministic step verifies its outcome with
`expect`, so an engine may act as soon as the target holds still. An agent reads the screen right after acting, so an engine may wait
for the transition to end first. .NET-only: upstream's `origin` is the string union `'test' | 'agent'`.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/OperationContext.cs)

## Fields

<a id="E2E_Engine_OperationOrigin_Agent"></a>

### Agent


```csharp
Agent = 1
```


The agent.

**Value:** [OperationOrigin](./)

<a id="E2E_Engine_OperationOrigin_Test"></a>

### Test


```csharp
Test = 0
```


A test's own step.

**Value:** [OperationOrigin](./)
