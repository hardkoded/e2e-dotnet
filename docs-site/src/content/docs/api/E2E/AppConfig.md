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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L778)

## Properties

<a id="E2E_AppConfig_AppPath_"></a>
<a id="E2E_AppConfig_AppPath"></a>

### AppPath


```csharp
public string? AppPath { get; init; }
```


The build a device target installs, relative to the folder of `e2e.config.json`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L790)

<a id="E2E_AppConfig_BundleId_"></a>
<a id="E2E_AppConfig_BundleId"></a>

### BundleId


```csharp
public string? BundleId { get; init; }
```


The bundle id (iOS) or package (Android) of an installed app a device target launches.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L787)

<a id="E2E_AppConfig_Command_"></a>
<a id="E2E_AppConfig_Command"></a>

### Command


```csharp
public CommandConfig? Command { get; init; }
```


The command that starts the app (upstream's `app.command`), started before the first test and stopped when the process ends.
`{port}` in [Args](/api/E2E/CommandConfig/#E2E_CommandConfig_Args) and [Env](/api/E2E/CommandConfig/#E2E_CommandConfig_Env) is the port the app is served on. A
[Url](#E2E_AppConfig_Url) with port 0 asks for a free one, and needs this.

**Value:** [CommandConfig](/api/E2E/CommandConfig/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L797)

<a id="E2E_AppConfig_Environment_"></a>
<a id="E2E_AppConfig_Environment"></a>

### Environment


```csharp
public string? Environment { get; init; }
```


What the app is: `test`, `staging`, or `production` (upstream's `app.environment`). Unset, `test` for a
loopback, `.localhost`, or `.test` host, and `production` for any other host. The environment always joins the
cache key, so an identity never bleeds entries across environments.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L814)

<a id="E2E_AppConfig_Identity_"></a>
<a id="E2E_AppConfig_Identity"></a>

### Identity


```csharp
public string? Identity { get; init; }
```


The stable identity that keys the replay cache and saved sessions (upstream's `app.identity`). Unset, the URL's origin and
path as declared, else [BundleId](#E2E_AppConfig_BundleId), else [AppPath](#E2E_AppConfig_AppPath). An ephemeral per-deploy origin (a PR preview) can
declare the identity of the app it is a deployment of, and so share its entries.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L807)

<a id="E2E_AppConfig_LaunchArguments_"></a>
<a id="E2E_AppConfig_LaunchArguments"></a>

### LaunchArguments


```csharp
public IReadOnlyList<string>? LaunchArguments { get; init; }
```


Arguments every fresh launch of the app takes: process arguments on iOS, `am start` arguments on Android, the command line on Windows.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L817)

<a id="E2E_AppConfig_Permissions_"></a>
<a id="E2E_AppConfig_Permissions"></a>

### Permissions


```csharp
public IReadOnlyDictionary<string, AppPermissionState>? Permissions { get; init; }
```


Permissions the app holds before every fresh launch, by permission name (upstream's `app.permissions`). A device target
takes the names of `DevicePermission`.

**Value:** [IReadOnlyDictionary&lt;string, AppPermissionState&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L823)

<a id="E2E_AppConfig_ReadyUrl_"></a>
<a id="E2E_AppConfig_ReadyUrl"></a>

### ReadyUrl


```csharp
public string? ReadyUrl { get; init; }
```


The URL that answers once [Command](#E2E_AppConfig_Command) has started the app (upstream's `app.readyUrl`). Unset, [Url](#E2E_AppConfig_Url).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L800)

<a id="E2E_AppConfig_Url_"></a>
<a id="E2E_AppConfig_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


Base URL for `App.OpenAsync`, normalized: a missing scheme becomes `https://`, or `http://` for a loopback host.
Relative URLs resolve against it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L784)
