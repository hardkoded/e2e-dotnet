---
title: "CacheMode"
description: "How the replay cache is used."
editUrl: false
prev: false
next: false
sidebar:
  label: "CacheMode"
  hidden: true
---

<a id="E2E_CacheMode"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum CacheMode
```


How the replay cache is used.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L899)

## Fields

<a id="E2E_CacheMode_Off"></a>

### Off


```csharp
Off = 0
```


Never read or write recordings.

**Value:** [CacheMode](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L902)

<a id="E2E_CacheMode_ReadOnly"></a>

### ReadOnly


```csharp
ReadOnly = 1
```


Replay recordings, never write or delete them.

**Value:** [CacheMode](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L905)

<a id="E2E_CacheMode_ReadWrite"></a>

### ReadWrite


```csharp
ReadWrite = 2
```


Replay recordings, record verified acts, and delete unverified ones.

**Value:** [CacheMode](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L908)
