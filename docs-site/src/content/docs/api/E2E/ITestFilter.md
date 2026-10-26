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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/TestFilter.cs#L12)
