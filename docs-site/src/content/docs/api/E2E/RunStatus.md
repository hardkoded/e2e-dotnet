---
title: "RunStatus"
description: "How a run ended. Upstream's RunStatus, with the values a single run in the port can have."
editUrl: false
prev: false
next: false
sidebar:
  label: "RunStatus"
  hidden: true
---

<a id="E2E_RunStatus"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum RunStatus
```


How a run ended. Upstream's `RunStatus`, with the values a single run in the port can have.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Runner.cs#L8)

## Fields

<a id="E2E_RunStatus_Blocked"></a>

### Blocked


```csharp
Blocked = 2
```


Credentials, the environment, test setup, or the agent's own budget stopped the run, rather than the app misbehaving.

**Value:** [RunStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Runner.cs#L16)

<a id="E2E_RunStatus_Failed"></a>

### Failed


```csharp
Failed = 1
```


A test or an agent step failed.

**Value:** [RunStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Runner.cs#L13)

<a id="E2E_RunStatus_Passed"></a>

### Passed


```csharp
Passed = 0
```


**Value:** [RunStatus](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/Runner.cs#L10)
