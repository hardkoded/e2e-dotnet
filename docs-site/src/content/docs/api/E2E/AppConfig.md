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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs)

## Properties

<a id="E2E_AppConfig_AppPath_"></a>
<a id="E2E_AppConfig_AppPath"></a>

### AppPath


```csharp
public string? AppPath { get; init; }
```


The build a device target installs, relative to the folder of `e2e.config.json`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_AppConfig_BundleId_"></a>
<a id="E2E_AppConfig_BundleId"></a>

### BundleId


```csharp
public string? BundleId { get; init; }
```


The bundle id (iOS) or package (Android) of an installed app a device target launches.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

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

<a id="E2E_AppConfig_LaunchArgumentValues_"></a>
<a id="E2E_AppConfig_LaunchArgumentValues"></a>

### LaunchArgumentValues


```csharp
public IReadOnlyDictionary<string, LaunchArgumentValue?>? LaunchArgumentValues { get; init; }
```


Arguments every fresh launch of the app takes, as an object of argument name (no leading dash, the engine adds it) to a string, number, boolean,
or list of strings: `NSUserDefaults` arguments on iOS, `am start` extras of each value's type on Android. A null value leaves its entry out.
Only an engine that declares [LaunchArgumentValues](/api/E2E.Engine/EngineSpec/#E2E_Engine_EngineSpec_LaunchArgumentValues) takes it: for any other engine the object is `INVALID_CONFIG` at config load.
The object form of upstream's `app.launchArguments`; .NET-only property, because C# has no union type.

**Value:** [IReadOnlyDictionary&lt;string, LaunchArgumentValue?&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_AppConfig_LaunchArguments_"></a>
<a id="E2E_AppConfig_LaunchArguments"></a>

### LaunchArguments


```csharp
public IReadOnlyList<string>? LaunchArguments { get; init; }
```


Arguments every fresh launch of the app takes, as a list of strings: process arguments on iOS, `am start` arguments on Android, the command
line on Windows. Upstream's `app.launchArguments` takes this list or an object; the object is [LaunchArgumentValues](#E2E_AppConfig_LaunchArgumentValues), and at most one is set.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_AppConfig_LinkBase_"></a>
<a id="E2E_AppConfig_LinkBase"></a>

### LinkBase


```csharp
public string? LinkBase { get; init; }
```


The base `App.OpenAsync(path)` joins a path to on a device target (upstream's `app.linkBase`): an absolute URL such as
`https://shop.example.com`, or a bare scheme such as `shopapp://`, with no query or fragment. The joined link opens in the pinned
app; an absolute link opens as given. Needs an engine that opens links, such as the device engine.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_AppConfig_Permissions_"></a>
<a id="E2E_AppConfig_Permissions"></a>

### Permissions


```csharp
public IReadOnlyDictionary<string, AppPermissionState>? Permissions { get; init; }
```


Permissions the app holds, by permission name (upstream's `app.permissions`). A device target sets each before every fresh
launch and takes the names of `DevicePermission`. A web target takes [Grant](/api/E2E/AppPermissionState/#E2E_AppPermissionState_Grant) only, for the names its
browser grants (Playwright's), and every page of the attempt holds the grant; headless Chromium refuses what is left out.

**Value:** [IReadOnlyDictionary&lt;string, AppPermissionState&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_AppConfig_ReadyUrl_"></a>
<a id="E2E_AppConfig_ReadyUrl"></a>

### ReadyUrl


```csharp
public string? ReadyUrl { get; init; }
```


The URL that answers once [Command](#E2E_AppConfig_Command) has started the app (upstream's `app.readyUrl`). Unset, [Url](#E2E_AppConfig_Url).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_AppConfig_Url_"></a>
<a id="E2E_AppConfig_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


Base URL for `App.OpenAsync`, normalized: a missing scheme becomes `https://`, or `http://` for a loopback host.
Relative URLs resolve against it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
