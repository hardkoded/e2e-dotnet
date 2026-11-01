---
title: "VerdictTool"
description: "Upstream's VerdictTool: the done tool and the verdict it collected."
editUrl: false
prev: false
next: false
sidebar:
  label: "VerdictTool"
  hidden: true
---

<a id="E2E_VerdictTool"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class VerdictTool
```


Upstream's `VerdictTool`: the `done` tool and the verdict it collected.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L122)

## Properties

<a id="E2E_VerdictTool_Concluded_"></a>
<a id="E2E_VerdictTool_Concluded"></a>

### Concluded


```csharp
public bool Concluded { get; }
```


True once a verdict landed.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L141)

<a id="E2E_VerdictTool_Description_"></a>
<a id="E2E_VerdictTool_Description"></a>

### Description


```csharp
public string Description { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L133)

<a id="E2E_VerdictTool_InputSchema_"></a>
<a id="E2E_VerdictTool_InputSchema"></a>

### InputSchema


```csharp
public JsonElement InputSchema { get; }
```


**Value:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L135)

<a id="E2E_VerdictTool_Name_"></a>
<a id="E2E_VerdictTool_Name"></a>

### Name


```csharp
public string Name { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L131)

<a id="E2E_VerdictTool_Verdict_"></a>
<a id="E2E_VerdictTool_Verdict"></a>

### Verdict


```csharp
public StepVerdict? Verdict { get; }
```


The verdict once the model called the tool with an accepted one.

**Value:** `StepVerdict?`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L138)

## Methods

<a id="E2E_VerdictTool_Execute_"></a>
<a id="E2E_VerdictTool_Execute_System_Text_Json_JsonElement_"></a>

### Execute(JsonElement)


```csharp
public string Execute(JsonElement input)
```


Takes a call. A refused verdict answers `Rejected: ` and why, so the model can call again.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `input` | [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L144)
