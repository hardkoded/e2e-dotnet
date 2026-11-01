---
title: "GrammarTool"
description: "One action tool of the built-in agent's grammar: what a model is told about it, and what runs when it calls it."
editUrl: false
prev: false
next: false
sidebar:
  label: "GrammarTool"
  hidden: true
---

<a id="E2E_GrammarTool"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class GrammarTool
```


One action tool of the built-in agent's grammar: what a model is told about it, and what runs when it calls it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L91)

## Properties

<a id="E2E_GrammarTool_Description_"></a>
<a id="E2E_GrammarTool_Description"></a>

### Description


```csharp
public string Description { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L106)

<a id="E2E_GrammarTool_InputSchema_"></a>
<a id="E2E_GrammarTool_InputSchema"></a>

### InputSchema


```csharp
public JsonElement InputSchema { get; }
```


The JSON schema of the tool's input object.

**Value:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L109)

<a id="E2E_GrammarTool_Name_"></a>
<a id="E2E_GrammarTool_Name"></a>

### Name


```csharp
public string Name { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L104)

<a id="E2E_GrammarTool_ReadOnly_"></a>
<a id="E2E_GrammarTool_ReadOnly"></a>

### ReadOnly


```csharp
public bool ReadOnly { get; }
```


True for a tool that changes nothing in the app, so an assertion may use it.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L112)

## Methods

<a id="E2E_GrammarTool_ExecuteAsync_"></a>
<a id="E2E_GrammarTool_ExecuteAsync_System_Text_Json_JsonElement_System_Threading_CancellationToken_"></a>

### ExecuteAsync(JsonElement, CancellationToken)


```csharp
public Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken = default)
```


Runs the tool and answers the screen after it. A refusal or a failed action is thrown as an [E2EException](/api/E2E/E2EException/), and a
hard stop of the step as an [AgentException](/api/E2E/AgentException/) with a runtime code.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `input` | [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L118)
