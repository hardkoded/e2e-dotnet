---
title: "BeforeActionHook"
description: "The onBeforeAction hook of an agent.act call."
editUrl: false
prev: false
next: false
sidebar:
  label: "BeforeActionHook"
  hidden: true
---

<a id="E2E_BeforeActionHook"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public delegate Task BeforeActionHook(BeforeActionEvent beforeActionEvent)
```


The `onBeforeAction` hook of an `agent.act` call.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `beforeActionEvent` | [BeforeActionEvent](/api/E2E/BeforeActionEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs)
