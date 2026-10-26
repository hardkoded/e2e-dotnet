---
title: "ExecutorAttempt"
description: "The attempt a step belongs to: identity and lifecycle. It lets an executor keep state across the ActAsync calls of one test, a running conversation or a plan..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ExecutorAttempt"
  hidden: true
---

<a id="E2E_ExecutorAttempt"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class ExecutorAttempt
```


The attempt a step belongs to: identity and lifecycle. It lets an executor keep state across the `ActAsync` calls of
one test, a running conversation or a plan or notes, without mixing it with the next test's or a retry's.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L185)

## Properties

<a id="E2E_ExecutorAttempt_AttemptId_"></a>
<a id="E2E_ExecutorAttempt_AttemptId"></a>

### AttemptId


```csharp
public required string AttemptId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L189)

<a id="E2E_ExecutorAttempt_CancellationToken_"></a>
<a id="E2E_ExecutorAttempt_CancellationToken"></a>

### CancellationToken


```csharp
public CancellationToken CancellationToken { get; init; }
```


Ends when the attempt ends: passed, failed, or cancelled. An executor that holds state for the attempt releases it
on this token. Upstream's `signal`.

**Value:** [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L204)

<a id="E2E_ExecutorAttempt_Index_"></a>
<a id="E2E_ExecutorAttempt_Index"></a>

### Index


```csharp
public int Index { get; init; }
```


The zero-based retry index of this attempt.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L198)

<a id="E2E_ExecutorAttempt_Memory_"></a>
<a id="E2E_ExecutorAttempt_Memory"></a>

### Memory


```csharp
public IDictionary<string, object?> Memory { get; init; }
```


Scratch space for the attempt, kept by the runtime for the attempt's life and never persisted, reported, or shown to
a model by the runtime: what goes in is the executor's, redaction included. Keyed, so several executors, or an
executor and its tools, can share it.

**Value:** [IDictionary&lt;string, object&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.idictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L211)

<a id="E2E_ExecutorAttempt_TestId_"></a>
<a id="E2E_ExecutorAttempt_TestId"></a>

### TestId


```csharp
public required string TestId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L187)

<a id="E2E_ExecutorAttempt_TitlePath_"></a>
<a id="E2E_ExecutorAttempt_TitlePath"></a>

### TitlePath


```csharp
public IReadOnlyList<string> TitlePath { get; init; }
```


The test's title path, outermost `describe` first, redacted; empty outside a test (a session with no test file, as for
`e2e mcp`).

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Executor.cs#L195)
