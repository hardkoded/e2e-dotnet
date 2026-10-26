---
title: "PollExpectationExtensions"
description: "NUnit constraints for Expect.Poll, in place of the upstream value matchers."
editUrl: false
prev: false
next: false
sidebar:
  label: "PollExpectationExtensions"
  hidden: true
---

<a id="E2E_NUnit_PollExpectationExtensions"></a>

Namespace: [E2E.NUnit](/api/E2E.NUnit/)


```csharp
public static class PollExpectationExtensions
```


NUnit constraints for `Expect.Poll`, in place of the upstream value matchers.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/PollExpectationExtensions.cs#L10)

## Methods

<a id="E2E_NUnit_PollExpectationExtensions_ToMatchAsync_"></a>
<a id="E2E_NUnit_PollExpectationExtensions_ToMatchAsync__1_E2E_PollExpectation___0__NUnit_Framework_Constraints_IResolveConstraint_System_Threading_CancellationToken_"></a>

### `ToMatchAsync<T>(PollExpectation<T>, IResolveConstraint, CancellationToken)`


```csharp
public static Task ToMatchAsync<T>(this PollExpectation<T> poll, IResolveConstraint constraint, CancellationToken cancellationToken = default)
```


Re-reads the value until &lt;code class="paramref"&gt;constraint&lt;/code&gt; holds, such as
`Is.GreaterThan(3)` or `Has.Count.EqualTo(2)`.

**Type parameters**

| Name | Description |
| --- | --- |
| `T` |  |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `poll` | `PollExpectation&lt;T&gt;` |  |
| `constraint` | `IResolveConstraint` |  |
| `cancellationToken` | [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) |  |

**Returns:** [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.NUnit/PollExpectationExtensions.cs#L16)
