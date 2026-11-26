---
title: "LaunchApp"
description: "The app a host drives: what app.open() opens on a browser, or launches on a device. Upstream's LaunchApp (core/launch.ts), a pick of the target's app."
editUrl: false
prev: false
next: false
sidebar:
  label: "LaunchApp"
  hidden: true
---

<a id="E2E_LaunchApp"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record LaunchApp : IEquatable<LaunchApp>
```


The app a host drives: what `app.open()` opens on a browser, or launches on a device. Upstream's `LaunchApp` (`core/launch.ts`), a pick of the target's app.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;LaunchApp&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/Launch.cs)

## Properties

<a id="E2E_LaunchApp_AppPath_"></a>
<a id="E2E_LaunchApp_AppPath"></a>

### AppPath


```csharp
public string? AppPath { get; init; }
```


The build a device installs.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_LaunchApp_BundleId_"></a>
<a id="E2E_LaunchApp_BundleId"></a>

### BundleId


```csharp
public string? BundleId { get; init; }
```


The bundle id or package of an installed app a device launches.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_LaunchApp_Identity_"></a>
<a id="E2E_LaunchApp_Identity"></a>

### Identity


```csharp
public string? Identity { get; init; }
```


The stable identity a saved state is bound to; see [Identity](/api/E2E/AppConfig/#E2E_AppConfig_Identity).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_LaunchApp_LaunchArgumentValues_"></a>
<a id="E2E_LaunchApp_LaunchArgumentValues"></a>

### LaunchArgumentValues


```csharp
public IReadOnlyDictionary<string, LaunchArgumentValue?>? LaunchArgumentValues { get; init; }
```


Arguments every fresh launch of the app takes, as an object of argument name to value; see [LaunchArgumentValues](/api/E2E/AppConfig/#E2E_AppConfig_LaunchArgumentValues). At most one of this and [LaunchArguments](#E2E_LaunchApp_LaunchArguments) is set.

**Value:** [IReadOnlyDictionary&lt;string, LaunchArgumentValue?&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_LaunchApp_LaunchArguments_"></a>
<a id="E2E_LaunchApp_LaunchArguments"></a>

### LaunchArguments


```csharp
public IReadOnlyList<string>? LaunchArguments { get; init; }
```


Arguments every fresh launch of the app takes, as a list.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_LaunchApp_LinkBase_"></a>
<a id="E2E_LaunchApp_LinkBase"></a>

### LinkBase


```csharp
public string? LinkBase { get; init; }
```


The base `App.OpenAsync(path)` joins a path to on a device; see [LinkBase](/api/E2E/AppConfig/#E2E_AppConfig_LinkBase). The engine must open links, as the device engine does.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_LaunchApp_Permissions_"></a>
<a id="E2E_LaunchApp_Permissions"></a>

### Permissions


```csharp
public IReadOnlyDictionary<string, AppPermissionState>? Permissions { get; init; }
```


Permissions the app holds, by name.

**Value:** [IReadOnlyDictionary&lt;string, AppPermissionState&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_LaunchApp_Url_"></a>
<a id="E2E_LaunchApp_Url"></a>

### Url


```csharp
public string? Url { get; init; }
```


The app's URL on a browser.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
