---
title: "PollConditionOptions"
description: "What PollConditionAsync(PollConditionOptions) polls, and how it ends."
editUrl: false
prev: false
next: false
sidebar:
  label: "PollConditionOptions"
  hidden: true
---

<a id="E2E_Internal_PollConditionOptions"></a>

Namespace: [E2E.Internal](/api/E2E.Internal/)


```csharp
public sealed class PollConditionOptions
```


What [PollConditionAsync(PollConditionOptions)](/api/E2E.Internal/Time/#E2E_Internal_Time_PollConditionAsync_E2E_Internal_PollConditionOptions_) polls, and how it ends.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Internal/Time.cs)

## Properties

<a id="E2E_Internal_PollConditionOptions_Clock_"></a>
<a id="E2E_Internal_PollConditionOptions_Clock"></a>

### Clock


```csharp
public required TimeProvider Clock { get; init; }
```


**Value:** [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

<a id="E2E_Internal_PollConditionOptions_Deadline_"></a>
<a id="E2E_Internal_PollConditionOptions_Deadline"></a>

### Deadline


```csharp
public required Deadline Deadline { get; init; }
```


**Value:** [Deadline](/api/E2E.Internal/Deadline/)

<a id="E2E_Internal_PollConditionOptions_Evaluate_"></a>
<a id="E2E_Internal_PollConditionOptions_Evaluate"></a>

### Evaluate


```csharp
public required Func<Task<bool?>> Evaluate { get; init; }
```


Evaluates the positive condition once, every read bounded by the deadline: a read on a longer budget outlasts
the poll. Returns null when the condition cannot be evaluated yet: the positive poll keeps waiting and the
negation grace window resets.

**Value:** [Func&lt;Task&lt;bool?&gt;&gt;](https://learn.microsoft.com/dotnet/api/system.func-1)

<a id="E2E_Internal_PollConditionOptions_Interval_"></a>
<a id="E2E_Internal_PollConditionOptions_Interval"></a>

### Interval


```csharp
public TimeSpan Interval { get; init; }
```


The longest pause between reads.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_Internal_PollConditionOptions_Negated_"></a>
<a id="E2E_Internal_PollConditionOptions_Negated"></a>

### Negated


```csharp
public required bool Negated { get; init; }
```


**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Internal_PollConditionOptions_OnTimeout_"></a>
<a id="E2E_Internal_PollConditionOptions_OnTimeout"></a>

### OnTimeout


```csharp
public required Func<Exception?, Exception> OnTimeout { get; init; }
```


Builds the poll's failure; the argument is the read the deadline cut off, when one did.

**Value:** [Func&lt;Exception, Exception&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

<a id="E2E_Internal_PollConditionOptions_Token_"></a>
<a id="E2E_Internal_PollConditionOptions_Token"></a>

### Token


```csharp
public required CancellationToken Token { get; init; }
```


**Value:** [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)
