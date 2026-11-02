---
title: "ExecutorStep"
description: "One step handed to an executor. An Act plans and executes a flow. An Assert judges a condition and must not change the app."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorStep"
  hidden: true
---

<a id="E2E_ExecutorStep"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ExecutorStep
```


One step handed to an executor. An [Act](/api/E2E/ExecutorStepKind/#E2E_ExecutorStepKind_Act) plans and executes a flow. An
[Assert](/api/E2E/ExecutorStepKind/#E2E_ExecutorStepKind_Assert) judges a condition and must not change the app.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs)

## Properties

<a id="E2E_ExecutorStep_Index_"></a>
<a id="E2E_ExecutorStep_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


The zero-based position of this step among the agent steps of the attempt.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_ExecutorStep_Instruction_"></a>
<a id="E2E_ExecutorStep_Instruction"></a>

### Instruction


```csharp
public required string Instruction { get; init; }
```


The instruction or assertion the test passed.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ExecutorStep_Kind_"></a>
<a id="E2E_ExecutorStep_Kind"></a>

### Kind


```csharp
public required ExecutorStepKind Kind { get; init; }
```


**Value:** [ExecutorStepKind](/api/E2E/ExecutorStepKind/)

<a id="E2E_ExecutorStep_Params_"></a>
<a id="E2E_ExecutorStep_Params"></a>

### Params


```csharp
public IReadOnlyDictionary<string, JsonElement>? Params { get; init; }
```


The call params as JSON. A [Secret](/api/E2E/Secret/) is projected to `{"kind":"secret","name":...,"purpose":...}`: the
plaintext never reaches the executor, which fills fields through `TypeSecretAsync`.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_ExecutorStep_Secrets_"></a>
<a id="E2E_ExecutorStep_Secrets"></a>

### Secrets


```csharp
public IReadOnlyList<ExecutorSecret> Secrets { get; init; }
```


The secrets declared in the params, which `TypeSecretAsync` may fill.

**Value:** [IReadOnlyList&lt;ExecutorSecret&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
