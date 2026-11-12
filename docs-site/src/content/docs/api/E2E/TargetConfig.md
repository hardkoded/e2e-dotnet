---
title: "TargetConfig"
description: "One entry of targets. The port drives the web, ios, and android platforms."
editUrl: false
prev: false
next: false
sidebar:
  label: "TargetConfig"
  hidden: true
---

<a id="E2E_TargetConfig"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class TargetConfig
```


One entry of `targets`. The port drives the `web`, `ios`, and `android` platforms.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs)

## Properties

<a id="E2E_TargetConfig_App_"></a>
<a id="E2E_TargetConfig_App"></a>

### App


```csharp
public required AppConfig App { get; init; }
```


**Value:** [AppConfig](/api/E2E/AppConfig/)

<a id="E2E_TargetConfig_Engine_"></a>
<a id="E2E_TargetConfig_Engine"></a>

### Engine


```csharp
public IEngine? Engine { get; init; }
```


The engine this target runs, built in code (upstream's `engine` of a target, `defineEngine(...)`); null for a JSON target, whose
engine its platform chooses. `WithTarget` sets it. A run resolves the config with it: the secrets its options
hold are checked against `secrets`, and its name, version, platform, and capabilities enter the config digest.

**Value:** [IEngine](/api/E2E.Engine/IEngine/)

<a id="E2E_TargetConfig_Name_"></a>
<a id="E2E_TargetConfig_Name"></a>

### Name


```csharp
public required string Name { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_TargetConfig_Platform_"></a>
<a id="E2E_TargetConfig_Platform"></a>

### Platform


```csharp
public required string Platform { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_TargetConfig_Trace_"></a>
<a id="E2E_TargetConfig_Trace"></a>

### Trace


```csharp
public ResolvedRecording Trace { get; init; }
```


Which attempts write `trace.md`: `E2E_TRACE`, else this target's `trace`, else the config's, else `retain-on-failure`.

**Value:** [ResolvedRecording](/api/E2E/ResolvedRecording/)

<a id="E2E_TargetConfig_Video_"></a>
<a id="E2E_TargetConfig_Video"></a>

### Video


```csharp
public ResolvedRecording Video { get; init; }
```


Which attempts record a video: `E2E_VIDEO`, else this target's `video`, else the config's, else `off`.

**Value:** [ResolvedRecording](/api/E2E/ResolvedRecording/)
