---
title: "AppPermissionState"
description: "What a permission is set to before a launch (upstream's AppPermissionState): held, refused, or not asked for yet, so the OS asks again."
editUrl: false
prev: false
next: false
sidebar:
  label: "AppPermissionState"
  hidden: true
---

<a id="E2E_AppPermissionState"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public enum AppPermissionState
```


What a permission is set to before a launch (upstream's `AppPermissionState`): held, refused, or not asked for yet, so the OS asks again.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L841)

## Fields

<a id="E2E_AppPermissionState_Deny"></a>

### Deny


```csharp
Deny = 1
```


The permission is refused.

**Value:** [AppPermissionState](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L847)

<a id="E2E_AppPermissionState_Grant"></a>

### Grant


```csharp
Grant = 0
```


The app holds the permission.

**Value:** [AppPermissionState](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L844)

<a id="E2E_AppPermissionState_Reset"></a>

### Reset


```csharp
Reset = 2
```


The permission is not asked for yet.

**Value:** [AppPermissionState](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L850)
