---
title: "AppConfig"
description: "AppConfig in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "AppConfig"
  hidden: true
---

<a id="E2E_AppConfig"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class AppConfig
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L700)

## Properties

<a id="E2E_AppConfig_AppPath_"></a>
<a id="E2E_AppConfig_AppPath"></a>

### AppPath


```csharp
public string? AppPath { get; init; }
```


The build a device target installs, relative to the folder of `e2e.config.json`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L712)

<a id="E2E_AppConfig_BundleId_"></a>
<a id="E2E_AppConfig_BundleId"></a>

### BundleId


```csharp
public string? BundleId { get; init; }
```


The bundle id (iOS) or package (Android) of an installed app a device target launches.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L709)

<a id="E2E_AppConfig_Url_"></a>
<a id="E2E_AppConfig_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


Base URL for `App.OpenAsync`, normalized: a missing scheme becomes `https://`, or `http://` for a loopback host.
Relative URLs resolve against it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L706)
