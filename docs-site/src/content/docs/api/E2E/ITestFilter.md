---
title: "ITestFilter"
description: "Leaves collected tests out of a run before anything starts (upstream's TestFilter, types.ts). A host that selects tests calls it through TestFilters.ApplyTes..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ITestFilter"
  hidden: true
---

<a id="E2E_ITestFilter"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public interface ITestFilter
```


Leaves collected tests out of a run before anything starts (upstream's `TestFilter`, `types.ts`). A host that selects
tests calls it through `TestFilters.ApplyTestFilters`, at most once. A call that throws, returns an invalid result, or
runs past 5 minutes leaves nothing out. .NET-only: the interface prefix and the `Async` suffix.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs)

## Properties

<a id="E2E_ITestFilter_Name_"></a>
<a id="E2E_ITestFilter_Name"></a>

### Name


```csharp
string Name { get; }
```


Names the filter in the report and in errors.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_ITestFilter_FilterAsync_"></a>
<a id="E2E_ITestFilter_FilterAsync_System_Collections_Generic_IReadOnlyList_E2E_TestFilterCandidate__E2E_TestFilterContext_"></a>

### `FilterAsync(IReadOnlyList<TestFilterCandidate>, TestFilterContext)`


```csharp
Task<TestFilterResult> FilterAsync(IReadOnlyList<TestFilterCandidate> tests, TestFilterContext context)
```


Decides which of &lt;code class="paramref"&gt;tests&lt;/code&gt; to leave out; every test the result does not name runs.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `tests` | [IReadOnlyList&lt;TestFilterCandidate&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1) |  |
| `context` | [TestFilterContext](/api/E2E/TestFilterContext/) |  |

**Returns:** [Task&lt;TestFilterResult&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
