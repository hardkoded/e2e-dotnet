---
title: "LaunchOptions"
description: "What LaunchAsync is given. Upstream's LaunchOptions (core/launch.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "LaunchOptions"
  hidden: true
---

<a id="E2E_LaunchOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record LaunchOptions : IEquatable<LaunchOptions>
```


What `LaunchAsync` is given. Upstream's `LaunchOptions` (`core/launch.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;LaunchOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/Launch.cs)

## Properties

<a id="E2E_LaunchOptions_App_"></a>
<a id="E2E_LaunchOptions_App"></a>

### App


```csharp
public LaunchApp? App { get; init; }
```


The app: [Url](/api/E2E/LaunchApp/#E2E_LaunchApp_Url) on a browser; [BundleId](/api/E2E/LaunchApp/#E2E_LaunchApp_BundleId) or [AppPath](/api/E2E/LaunchApp/#E2E_LaunchApp_AppPath) on a device.

**Value:** [LaunchApp](/api/E2E/LaunchApp/)

<a id="E2E_LaunchOptions_ArtifactsDir_"></a>
<a id="E2E_LaunchOptions_ArtifactsDir"></a>

### ArtifactsDir


```csharp
public string? ArtifactsDir { get; init; }
```


Where screenshots, downloads, and recordings are written, from [Cwd](#E2E_LaunchOptions_Cwd). Default: a fresh directory under the system temp directory,
deleted when the host closes.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_LaunchOptions_Cwd_"></a>
<a id="E2E_LaunchOptions_Cwd"></a>

### Cwd


```csharp
public string? Cwd { get; init; }
```


The directory upload paths and relative files resolve against, [ArtifactsDir](#E2E_LaunchOptions_ArtifactsDir) included; default the current directory.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_LaunchOptions_Env_"></a>
<a id="E2E_LaunchOptions_Env"></a>

### Env


```csharp
public IReadOnlyDictionary<string, string?>? Env { get; init; }
```


The environment the engine reads; default the process environment.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_LaunchOptions_OnNotice_"></a>
<a id="E2E_LaunchOptions_OnNotice"></a>

### OnNotice


```csharp
public Action<string>? OnNotice { get; init; }
```


Progress outside operations: provisioning, downloads, device leases. Must not throw.

**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_LaunchOptions_Secrets_"></a>
<a id="E2E_LaunchOptions_Secrets"></a>

### Secrets


```csharp
public IReadOnlyDictionary<string, string>? Secrets { get; init; }
```


Secret values by name, at least 6 characters each. Fill one through [Secret(string)](/api/E2E/Host/#E2E_Host_Secret_System_String_); an agent receives it by name only. The values
are redacted from what `Screen.Tree()` and the agent read, and from the errors this host throws.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_LaunchOptions_Timeout_"></a>
<a id="E2E_LaunchOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


Default timeout of one operation: an action, a wait, a read. Without it an action gets 30 seconds. An agent step has its own, `ActAsync`'s
timeout. There is no session deadline.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)
