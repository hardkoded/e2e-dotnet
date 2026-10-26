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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L708)

## Properties

<a id="E2E_AppConfig_AppPath_"></a>
<a id="E2E_AppConfig_AppPath"></a>

### AppPath


```csharp
public string? AppPath { get; init; }
```


The build a device target installs, relative to the folder of `e2e.config.json`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L720)

<a id="E2E_AppConfig_BundleId_"></a>
<a id="E2E_AppConfig_BundleId"></a>

### BundleId


```csharp
public string? BundleId { get; init; }
```


The bundle id (iOS) or package (Android) of an installed app a device target launches.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L717)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L727)

<a id="E2E_AppConfig_ReadyUrl_"></a>
<a id="E2E_AppConfig_ReadyUrl"></a>

### ReadyUrl


```csharp
public string? ReadyUrl { get; init; }
```


The URL that answers once [Command](#E2E_AppConfig_Command) has started the app (upstream's `app.readyUrl`). Unset, [Url](#E2E_AppConfig_Url).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L730)

<a id="E2E_AppConfig_Url_"></a>
<a id="E2E_AppConfig_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


Base URL for `App.OpenAsync`, normalized: a missing scheme becomes `https://`, or `http://` for a loopback host.
Relative URLs resolve against it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L714)
