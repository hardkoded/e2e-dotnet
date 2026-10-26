---
title: "BrowserProviderScope"
description: "How long a lease lives. Worker (the default) is one browser per worker slot, acquired in prepare and released in finish. Attempt is a fresh browser per attem..."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserProviderScope"
  hidden: true
---

<a id="E2E_Engine_BrowserProviderScope"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public enum BrowserProviderScope
```


How long a lease lives. [Worker](#E2E_Engine_BrowserProviderScope_Worker) (the default) is one browser per worker slot, acquired in prepare and
released in finish. [Attempt](#E2E_Engine_BrowserProviderScope_Attempt) is a fresh browser per attempt, acquired when the attempt starts and
released when it ends, reconnected through the lease's [ReconnectEndpoint](/api/E2E.Engine/BrowserLease/#E2E_Engine_BrowserLease_ReconnectEndpoint) after a
transport drop.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L19)

## Fields

<a id="E2E_Engine_BrowserProviderScope_Attempt"></a>

### Attempt


```csharp
Attempt = 1
```


**Value:** [BrowserProviderScope](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L22)

<a id="E2E_Engine_BrowserProviderScope_Worker"></a>

### Worker


```csharp
Worker = 0
```


**Value:** [BrowserProviderScope](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L21)
