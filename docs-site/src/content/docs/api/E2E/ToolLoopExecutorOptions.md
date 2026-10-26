---
title: "ToolLoopExecutorOptions"
description: "Upstream's ToolLoopExecutorOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "ToolLoopExecutorOptions"
  hidden: true
---

<a id="E2E_ToolLoopExecutorOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ToolLoopExecutorOptions
```


Upstream's `ToolLoopExecutorOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L45)

## Properties

<a id="E2E_ToolLoopExecutorOptions_BuildPrompt_"></a>
<a id="E2E_ToolLoopExecutorOptions_BuildPrompt"></a>

### BuildPrompt


```csharp
public required Func<StepExecutorContext, Task<string>> BuildPrompt { get; init; }
```


Builds the step's opening prompt: the first user message.

**Value:** [Func&lt;StepExecutorContext, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L67)

<a id="E2E_ToolLoopExecutorOptions_Model_"></a>
<a id="E2E_ToolLoopExecutorOptions_Model"></a>

### Model


```csharp
public IAgentModel? Model { get; init; }
```


The model the loop calls. Defaults to the one the agents entry configures ([Model](/api/E2E/StepExecutorContext/#E2E_StepExecutorContext_Model)). A
model brought here is also the agent's model for `assert`, `waitFor`, and `extract`, unless the entry names a judge.

**Value:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L55)

<a id="E2E_ToolLoopExecutorOptions_Name_"></a>
<a id="E2E_ToolLoopExecutorOptions_Name"></a>

### Name


```csharp
public required string Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L47)

<a id="E2E_ToolLoopExecutorOptions_PrepareMessages_"></a>
<a id="E2E_ToolLoopExecutorOptions_PrepareMessages"></a>

### PrepareMessages


```csharp
public Func<IReadOnlyList<ModelMessage>, PreparedTurn, PreparedMessages>? PrepareMessages { get; init; }
```


Between-turn history preparation, such as compaction. It runs before the loop's own notices, and the history it returns
carries forward to later turns.

**Value:** [Func&lt;IReadOnlyList&lt;ModelMessage&gt;, PreparedTurn, PreparedMessages&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L76)

<a id="E2E_ToolLoopExecutorOptions_ProviderOptions_"></a>
<a id="E2E_ToolLoopExecutorOptions_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


The provider options every model call carries. Defaults to the agents entry's.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L70)

<a id="E2E_ToolLoopExecutorOptions_System_"></a>
<a id="E2E_ToolLoopExecutorOptions_System"></a>

### System


```csharp
public Func<StepExecutorContext, string>? System { get; init; }
```


Executor-specific guidance for the model: what the tools are and how to address targets. The chassis appends the agent's
project context to it.

**Value:** [Func&lt;StepExecutorContext, string&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L61)

<a id="E2E_ToolLoopExecutorOptions_Tools_"></a>
<a id="E2E_ToolLoopExecutorOptions_Tools"></a>

### Tools


```csharp
public required Func<StepExecutorContext, ToolLoopHelpers, IReadOnlyList<ToolLoopTool>> Tools { get; init; }
```


Builds the step's tool vocabulary. The chassis adds `done`.

**Value:** [Func&lt;StepExecutorContext, ToolLoopHelpers, IReadOnlyList&lt;ToolLoopTool&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L64)

<a id="E2E_ToolLoopExecutorOptions_Version_"></a>
<a id="E2E_ToolLoopExecutorOptions_Version"></a>

### Version


```csharp
public string? Version { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/ToolLoop.cs#L49)
