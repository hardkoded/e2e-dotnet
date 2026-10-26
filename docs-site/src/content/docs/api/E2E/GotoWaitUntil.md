---
title: "GotoWaitUntil"
description: "When CancellationToken) counts a navigation as done."
editUrl: false
prev: false
next: false
sidebar:
  label: "GotoWaitUntil"
  hidden: true
---

<a id="E2E_GotoWaitUntil"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum GotoWaitUntil
```


When `CancellationToken)` counts a navigation as done.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/GotoOptions.cs#L8)

## Fields

<a id="E2E_GotoWaitUntil_DomContentLoaded"></a>

### DomContentLoaded


```csharp
DomContentLoaded = 1
```


The `DOMContentLoaded` event fired.

**Value:** [GotoWaitUntil](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/GotoOptions.cs#L14)

<a id="E2E_GotoWaitUntil_Load"></a>

### Load


```csharp
Load = 0
```


The `load` event fired. The default.

**Value:** [GotoWaitUntil](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/GotoOptions.cs#L11)

<a id="E2E_GotoWaitUntil_NetworkIdle"></a>

### NetworkIdle


```csharp
NetworkIdle = 2
```


The network was idle for at least 500 ms.

**Value:** [GotoWaitUntil](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/GotoOptions.cs#L17)
