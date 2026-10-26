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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L19)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L37)

<a id="E2E_ModelRequest_Messages_"></a>
<a id="E2E_ModelRequest_Messages"></a>

### Messages


```csharp
public required IReadOnlyList<ModelMessage> Messages { get; init; }
```


**Value:** [IReadOnlyList&lt;ModelMessage&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L23)

<a id="E2E_ModelRequest_ProviderOptions_"></a>
<a id="E2E_ModelRequest_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


The agent's provider options, keyed by provider, for the model to pass on. Null when none are set.
[OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/) adds the entry under its provider name to the request body.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L31)

<a id="E2E_ModelRequest_System_"></a>
<a id="E2E_ModelRequest_System"></a>

### System


```csharp
public required string System { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L21)

<a id="E2E_ModelRequest_Tools_"></a>
<a id="E2E_ModelRequest_Tools"></a>

### Tools


```csharp
public required IReadOnlyList<ModelTool> Tools { get; init; }
```


**Value:** [IReadOnlyList&lt;ModelTool&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Model.cs#L25)
