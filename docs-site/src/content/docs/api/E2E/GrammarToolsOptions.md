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

<a id="E2E_GrammarToolsOptions_SaveScreenshot_"></a>
<a id="E2E_GrammarToolsOptions_SaveScreenshot"></a>

### SaveScreenshot


```csharp
public Func<string, CancellationToken, Task<string>>? SaveScreenshot { get; init; }
```


Saves a screenshot to a file, given as the caller wrote it, and returns the absolute path. With it, `screenshot` takes an optional
`path`; `e2e mcp` sets it, the testing agent does not. Upstream's `saveScreenshot`; the port's takes the call's token too.

**Value:** [Func&lt;string, CancellationToken, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-3)
