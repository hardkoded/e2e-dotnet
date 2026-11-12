---
title: "ModelRequest"
description: "ModelRequest in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelRequest"
  hidden: true
---

<a id="E2E_ModelRequest"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ModelRequest
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs)

## Properties

<a id="E2E_ModelRequest_MarkLatest_"></a>
<a id="E2E_ModelRequest_MarkLatest"></a>

### MarkLatest


```csharp
public bool MarkLatest { get; init; }
```


Upstream's `ProviderHints.markLatest`, which only the act tool loop calls: the next turn repeats everything up
to the newest message, so [AnthropicModel](/api/E2E/AnthropicModel/) marks that message for caching. Judgments and extractions leave it off.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_ModelRequest_MaxRetries_"></a>
<a id="E2E_ModelRequest_MaxRetries"></a>

### MaxRetries


```csharp
public int? MaxRetries { get; init; }
```


Upstream's `ModelCall.maxRetries`: provider transport retries for this call. Null keeps `TransportRetries`.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_ModelRequest_Messages_"></a>
<a id="E2E_ModelRequest_Messages"></a>

### Messages


```csharp
public required IReadOnlyList<ModelMessage> Messages { get; init; }
```


**Value:** [IReadOnlyList&lt;ModelMessage&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_ModelRequest_ProviderOptions_"></a>
<a id="E2E_ModelRequest_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


The agent's provider options, keyed by provider, for the model to pass on. Null when none are set.
[OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/) adds the entry under its provider name to the request body.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_ModelRequest_Schema_"></a>
<a id="E2E_ModelRequest_Schema"></a>

### Schema


```csharp
public JsonElement? Schema { get; init; }
```


Upstream's `ModelCall.schema`: a closed response grammar the provider is asked to fill as structured output. When it is
set, [Tools](#E2E_ModelRequest_Tools) is empty and the model answers with one JSON object that matches it, as [Content](/api/E2E/ModelResponse/#E2E_ModelResponse_Content);
the agent validates that text itself. Null for a turn that offers tools. A custom [IAgentModel](/api/E2E/IAgentModel/) must handle it.

**Value:** [JsonElement?](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

<a id="E2E_ModelRequest_SchemaName_"></a>
<a id="E2E_ModelRequest_SchemaName"></a>

### SchemaName


```csharp
public string? SchemaName { get; init; }
```


Upstream's `ModelCall.schemaName`: the name sent with [Schema](#E2E_ModelRequest_Schema), for the providers that name a structured output.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelRequest_System_"></a>
<a id="E2E_ModelRequest_System"></a>

### System


```csharp
public required string System { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ModelRequest_ToolChoice_"></a>
<a id="E2E_ModelRequest_ToolChoice"></a>

### ToolChoice


```csharp
public ModelToolChoice? ToolChoice { get; init; }
```


Upstream's `ModelCall.toolChoice` (the AI SDK's `toolChoice`): how the model must use the tools it is offered. Null leaves
the choice to the provider, which is [Auto](/api/E2E/ModelToolChoice/#E2E_ModelToolChoice_Auto). The act tool loop asks for [Required](/api/E2E/ModelToolChoice/#E2E_ModelToolChoice_Required)
and names `done` on its forced-conclusion turns. A custom [IAgentModel](/api/E2E/IAgentModel/) that cannot force a tool may ignore it.

**Value:** [ModelToolChoice](/api/E2E/ModelToolChoice/)

<a id="E2E_ModelRequest_Tools_"></a>
<a id="E2E_ModelRequest_Tools"></a>

### Tools


```csharp
public required IReadOnlyList<ModelTool> Tools { get; init; }
```


**Value:** [IReadOnlyList&lt;ModelTool&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
