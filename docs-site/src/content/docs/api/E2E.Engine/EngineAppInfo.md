---
title: "EngineAppInfo"
description: "The app the harness resolved from the target's declaration, as an engine's doctor hook reads it: the origin and site policy, and what a device launches. The..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineAppInfo"
  hidden: true
---

<a id="E2E_Engine_EngineAppInfo"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public record EngineAppInfo : IEquatable<EngineAppInfo>
```


The app the harness resolved from the target's declaration, as an engine's doctor hook reads it: the origin and site policy, and what a device
launches. The URL itself stays with the harness. Upstream's `EngineAppInfo`, with the members the port's config declares.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineAppInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

**Derived:** [EngineAppDeclaration](/api/E2E.Engine/EngineAppDeclaration/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Properties

<a id="E2E_Engine_EngineAppInfo_AppPath_"></a>
<a id="E2E_Engine_EngineAppInfo_AppPath"></a>

### AppPath


```csharp
public string? AppPath { get; init; }
```


The build to install, as the target declared it; relative to [ProjectRoot](/api/E2E.Engine/EngineDoctorInfo/#E2E_Engine_EngineDoctorInfo_ProjectRoot).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineAppInfo_BundleId_"></a>
<a id="E2E_Engine_EngineAppInfo_BundleId"></a>

### BundleId


```csharp
public string? BundleId { get; init; }
```


The installed app to launch, as the target declared it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineAppInfo_LaunchArguments_"></a>
<a id="E2E_Engine_EngineAppInfo_LaunchArguments"></a>

### LaunchArguments


```csharp
public IReadOnlyList<string>? LaunchArguments { get; init; }
```


Arguments every fresh launch passes the app.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_Engine_EngineAppInfo_Origin_"></a>
<a id="E2E_Engine_EngineAppInfo_Origin"></a>

### Origin


```csharp
public string? Origin { get; init; }
```


The origin of the app's URL on the run's port (`scheme://host:port`, as `URL.origin` serializes it): where credentials an
engine injects into requests (headers, basic auth) go by default. Null for an app without a URL, and for a free port the run has not
assigned (`e2e doctor`).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineAppInfo_Permissions_"></a>
<a id="E2E_Engine_EngineAppInfo_Permissions"></a>

### Permissions


```csharp
public IReadOnlyDictionary<string, AppPermissionState>? Permissions { get; init; }
```


Permissions the app holds, by name; what each state means is the engine's.

**Value:** [IReadOnlyDictionary&lt;string, AppPermissionState&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_EngineAppInfo_Site_"></a>
<a id="E2E_Engine_EngineAppInfo_Site"></a>

### Site


```csharp
public string? Site { get; init; }
```


The site of the app's URL, its registrable domain. Null for an app without a URL, which is no policy at all.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
