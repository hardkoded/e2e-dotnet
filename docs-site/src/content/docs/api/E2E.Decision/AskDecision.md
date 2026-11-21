---
title: "AskDecision"
description: "Asks the decision model one request, or nothing once the step budget is spent (upstream's AskDecision)."
editUrl: false
prev: false
next: false
sidebar:
  label: "AskDecision"
  hidden: true
---

<a id="E2E_Decision_AskDecision"></a>

Namespace: [E2E.Decision](/api/E2E.Decision/)


```csharp
public delegate Task<IReadOnlyDictionary<string, Decision>?> AskDecision(DecisionRequest request)
```


Asks the decision model one request, or nothing once the step budget is spent (upstream's `AskDecision`).

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `request` | [DecisionRequest](/api/E2E.Decision/DecisionRequest/) |  |

**Returns:** [Task&lt;IReadOnlyDictionary&lt;string, Decision&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.Decision/Locator.cs)
