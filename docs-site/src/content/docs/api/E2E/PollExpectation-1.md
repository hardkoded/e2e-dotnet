---
title: "PollExpectation<T>"
description: "Re-reads a value until the chosen matcher holds or the timeout passes. A read that throws is retried, and its error is reported if the poll times out. A matc..."
editUrl: false
prev: false
next: false
sidebar:
  label: "PollExpectation<T>"
  hidden: true
---

<a id="E2E_PollExpectation_1"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class PollExpectation<T>
```


Re-reads a value until the chosen matcher holds or the timeout passes. A
read that throws is retried, and its error is reported if the poll times
out. A matcher that refuses the read value's type counts as not yet. With NUnit,
`ToMatchAsync` from `E2E.NUnit` takes any NUnit constraint.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Poll.cs#L30)

## Properties

<a id="E2E_PollExpectation_1_Not_"></a>
<a id="E2E_PollExpectation_1_Not"></a>

### Not


```csharp
public PollExpectation<T> Not { get; }
```


The same matchers, negated.

**Value:** [PollExpectation&lt;T&gt;](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Poll.cs#L47)

## Methods

<a id="E2E_PollExpectation_1_ToBeAsync_"></a>
<a id="E2E_PollExpectation_1_ToBeAsync__0_System_Threading_CancellationToken_"></a>

### ToBeAsync(T, CancellationToken)


```csharp
public Task ToBeAsync(T expected, CancellationToken cancellationToken = default)
```


Passes when the read value equals &lt;code class="paramref"&gt;expected&lt;/code&gt; by `Default`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `expected` | `T` |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Poll.cs#L50)

<a id="E2E_PollExpectation_1_ToSatisfyAsync_"></a>
<a id="E2E_PollExpectation_1_ToSatisfyAsync_System_Func__0_System_Boolean__System_String_System_Threading_CancellationToken_"></a>

### `ToSatisfyAsync(Func<T, bool>, string?, CancellationToken)`


```csharp
public Task ToSatisfyAsync(Func<T, bool> predicate, string? description = null, CancellationToken cancellationToken = default)
```


Passes when &lt;code class="paramref"&gt;predicate&lt;/code&gt; returns true for the read value. &lt;code class="paramref"&gt;description&lt;/code&gt; names the condition in the error.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `predicate` | [Func&lt;T, bool&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |
| `description` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Expect/Poll.cs#L60)
