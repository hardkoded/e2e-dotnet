---
title: "IBrowserProvider"
description: "A source of hosted browsers: the seam a hosted browser service plugs into (upstream BrowserProvider). In Worker scope CancellationToken) is called once per w..."
editUrl: false
prev: false
next: false
sidebar:
  label: "IBrowserProvider"
  hidden: true
---

<a id="E2E_Engine_IBrowserProvider"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public interface IBrowserProvider
```


A source of hosted browsers: the seam a hosted browser service plugs into (upstream `BrowserProvider`). In
[Worker](/api/E2E.Engine/BrowserProviderScope/#E2E_Engine_BrowserProviderScope_Worker) scope `CancellationToken)` is called once per worker slot, for every
slot at once, in the runner; a worker whose browser drops calls it again, from its own process, for a replacement.
In [Attempt](/api/E2E.Engine/BrowserProviderScope/#E2E_Engine_BrowserProviderScope_Attempt) scope every attempt start calls it from the worker. `CancellationToken)`
is called exactly once per lease, by the process that acquired it, also after an acquire of another slot failed; a worker
that dies first never calls it, and `CancellationToken)` releases what it left. A provider must not rely on
runner-process state to serve a request from a worker. Nothing here knows any vendor.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L138)
