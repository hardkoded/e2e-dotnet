---
title: "AgentTool"
description: "A project tool the agent offers to the model beside its own: upstream's defineTool. It declares whether it changes the application and which platforms it is..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AgentTool"
  hidden: true
---

<a id="E2E_AgentTool"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class AgentTool
```


A project tool the agent offers to the model beside its own: upstream's `defineTool`. It declares whether it
changes the application and which platforms it is offered on; the agent owns dispatch and accounting. The tool's
name is its key in [Tools](/api/E2E/AgentOptions/#E2E_AgentOptions_Tools).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L14)

## Constructors

<a id="E2E_AgentTool__ctor_"></a>
<a id="E2E_AgentTool__ctor_System_String_System_Text_Json_Nodes_JsonObject_System_Boolean_System_Func_System_Text_Json_Nodes_JsonObject_E2E_AgentToolContext_System_Threading_CancellationToken_System_Threading_Tasks_Task_System_String___System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `AgentTool(string, JsonObject, bool, Func<JsonObject, AgentToolContext, CancellationToken, Task<string>>, IReadOnlyList<string>?)`


```csharp
public AgentTool(string description, JsonObject inputSchema, bool mutates, Func<JsonObject, AgentToolContext, CancellationToken, Task<string>> executeAsync, IReadOnlyList<string>? platforms = null)
```


A tool that reaches the harness through [AgentToolContext](/api/E2E/AgentToolContext/): upstream's `getToolContext`. It can look at
the screen (a read-only tool only) and keep what it saw as a screenshot of the step.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `description` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What the model is told the tool does. |
| `inputSchema` | [JsonObject](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonobject) | The JSON schema of the tool's input. |
| `mutates` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) | Whether running the tool can change the application. A mutating tool takes an action slot and ends replay of the step. |
| `executeAsync` | [Func&lt;JsonObject, AgentToolContext, CancellationToken, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-4) | Runs the tool with its input as the model wrote it and the context of the step, and answers what the model reads. |
| `platforms` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The engine platforms the tool is offered on. Null means every platform. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L42)

<a id="E2E_AgentTool__ctor_System_String_System_Text_Json_Nodes_JsonObject_System_Boolean_System_Func_System_Text_Json_Nodes_JsonObject_System_Threading_CancellationToken_System_Threading_Tasks_Task_System_String___System_Collections_Generic_IReadOnlyList_System_String__"></a>

### `AgentTool(string, JsonObject, bool, Func<JsonObject, CancellationToken, Task<string>>, IReadOnlyList<string>?)`


```csharp
public AgentTool(string description, JsonObject inputSchema, bool mutates, Func<JsonObject, CancellationToken, Task<string>> executeAsync, IReadOnlyList<string>? platforms = null)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `description` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What the model is told the tool does. |
| `inputSchema` | [JsonObject](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonobject) | The JSON schema of the tool's input. |
| `mutates` | [bool](https://learn.microsoft.com/dotnet/api/system.boolean) | Whether running the tool can change the application. A mutating tool takes an action slot and ends replay of the step. |
| `executeAsync` | [Func&lt;JsonObject, CancellationToken, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3) | Runs the tool with its input as the model wrote it, and answers what the model reads. |
| `platforms` | [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) | The engine platforms the tool is offered on. Null means every platform. |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L23)

## Properties

<a id="E2E_AgentTool_Description_"></a>
<a id="E2E_AgentTool_Description"></a>

### Description


```csharp
public string Description { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L64)

<a id="E2E_AgentTool_ExecuteAsync_"></a>
<a id="E2E_AgentTool_ExecuteAsync"></a>

### ExecuteAsync


```csharp
public Func<JsonObject, CancellationToken, Task<string>> ExecuteAsync { get; }
```


Runs the tool with no step around it. A tool that reads its [AgentToolContext](/api/E2E/AgentToolContext/) fails there.

**Value:** [Func&lt;JsonObject, CancellationToken, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L77)

<a id="E2E_AgentTool_InputSchema_"></a>
<a id="E2E_AgentTool_InputSchema"></a>

### InputSchema


```csharp
public JsonObject InputSchema { get; }
```


**Value:** [JsonObject](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonobject)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L66)

<a id="E2E_AgentTool_Mutates_"></a>
<a id="E2E_AgentTool_Mutates"></a>

### Mutates


```csharp
public bool Mutates { get; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L68)

<a id="E2E_AgentTool_Platforms_"></a>
<a id="E2E_AgentTool_Platforms"></a>

### Platforms


```csharp
public IReadOnlyList<string>? Platforms { get; }
```


The platforms the tool is offered on. Null means every platform.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L74)

## Methods

<a id="E2E_AgentTool_AppliesTo_"></a>
<a id="E2E_AgentTool_AppliesTo_System_String_"></a>

### AppliesTo(string)


```csharp
public bool AppliesTo(string platform)
```


True when the tool is offered on &lt;code class="paramref"&gt;platform&lt;/code&gt;.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `platform` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTool.cs#L80)
