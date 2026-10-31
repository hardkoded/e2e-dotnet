---
title: "ActionHook"
description: "The onAction hook of an agent.act call."
editUrl: false
prev: false
next: false
sidebar:
  label: "ActionHook"
  hidden: true
---

<a id="E2E_ActionHook"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public delegate Task ActionHook(ActionEvent actionEvent)
```


The `onAction` hook of an `agent.act` call.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `actionEvent` | [ActionEvent](/api/E2E/ActionEvent/) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Agent.cs#L973)
