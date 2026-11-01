---
title: "GrammarToolOptions"
description: "Upstream's GrammarToolOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "GrammarToolOptions"
  hidden: true
---

<a id="E2E_GrammarToolOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class GrammarToolOptions
```


Upstream's `GrammarToolOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L81)

## Properties

<a id="E2E_GrammarToolOptions_Guard_"></a>
<a id="E2E_GrammarToolOptions_Guard"></a>

### Guard


```csharp
public Func<Func<Task<string>>, Task<string>>? Guard { get; init; }
```


Wraps the body of every tool call when its turn in the step's call order comes, so a call queued behind a verdict or a stop
can answer instead of running. Upstream's `guard`.

**Value:** [Func&lt;Func&lt;Task&lt;string&gt;&gt;, Task&lt;string&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/AgentPrimitives.cs#L87)
