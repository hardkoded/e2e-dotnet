---
title: "RelevanceTestCollectionOrderer"
description: "Starts the test classes with the tests the test filters rated likeliest to fail first (.NET-only: upstream's runner orders its work plans itself, and xUnit o..."
editUrl: false
prev: false
next: false
sidebar:
  label: "RelevanceTestCollectionOrderer"
  hidden: true
---

<a id="E2E_XUnit_V3_RelevanceTestCollectionOrderer"></a>

Namespace: [E2E.XUnit.V3](/api/E2E.XUnit.V3/)


```csharp
public sealed class RelevanceTestCollectionOrderer : ITestCollectionOrderer
```


Starts the test classes with the tests the test filters rated likeliest to fail first (.NET-only: upstream's runner orders its work plans
itself, and xUnit owns the order of the collections). A class ranks by the highest relevance band of its tests, as a file does in upstream's
`orderWorkPlans`; classes in one band keep xUnit's order. It orders only xUnit's default collection of one class. Register it for the
assembly: `[assembly: TestCollectionOrderer(typeof(E2E.XUnit.V3.RelevanceTestCollectionOrderer))]`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** `ITestCollectionOrderer`

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E.XUnit.V3/RelevanceTestCollectionOrderer.cs)

## Methods

<a id="E2E_XUnit_V3_RelevanceTestCollectionOrderer_OrderTestCollections_"></a>
<a id="E2E_XUnit_V3_RelevanceTestCollectionOrderer_OrderTestCollections__1_System_Collections_Generic_IReadOnlyCollection___0__"></a>

### `OrderTestCollections<TTestCollection>(IReadOnlyCollection<TTestCollection>)`


```csharp
public IReadOnlyCollection<TTestCollection> OrderTestCollections<TTestCollection>(IReadOnlyCollection<TTestCollection> testCollections) where TTestCollection : ITestCollection
```


Orders test collections for execution.

**Type parameters**

| Name | Description |
| --- | --- |
| `TTestCollection` | The type of the test collection to be ordered. Must derive from `ITestCollection`. |

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `testCollections` | [IReadOnlyCollection&lt;TTestCollection&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1) | The test collections to be ordered. |

**Returns:** [IReadOnlyCollection&lt;TTestCollection&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1) — The test collections in the order to be run.
