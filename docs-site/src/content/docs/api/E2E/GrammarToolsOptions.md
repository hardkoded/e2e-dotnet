---
title: "GrammarToolsOptions"
description: "What CreateGrammarTools runs a tool body under (upstream's GrammarToolOptions)."
editUrl: false
prev: false
next: false
sidebar:
  label: "GrammarToolsOptions"
  hidden: true
---

<a id="E2E_GrammarToolsOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class GrammarToolsOptions
```


What `CreateGrammarTools` runs a tool body under (upstream's `GrammarToolOptions`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentTools.cs)

## Properties

<a id="E2E_GrammarToolsOptions_Guard_"></a>
<a id="E2E_GrammarToolsOptions_Guard"></a>

### Guard


```csharp
public Func<Func<Task<string>>, Task<string>>? Guard { get; init; }
```


Wraps every tool body. By default the body runs as it is, so a hard stop of the step (budget, timeout, cancel) propagates out of the
tool, where the tool loop ends the step. Pass `body => helpers.GuardAsync(body)` to run each under the loop's policy instead,
or your own wrapper. Upstream's is generic over the body's result; the port's bodies answer the text the model reads.

**Value:** [Func&lt;Func&lt;Task&lt;string&gt;&gt;, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)
