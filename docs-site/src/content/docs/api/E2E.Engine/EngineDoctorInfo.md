---
title: "EngineDoctorInfo"
description: "The facts handed to an engine's doctor (DoctorAsync), once per target in the process e2e doctor runs in. Upstream's EngineDoctorInfo; its signal is the Cance..."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineDoctorInfo"
  hidden: true
---

<a id="E2E_Engine_EngineDoctorInfo"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineDoctorInfo : IEquatable<EngineDoctorInfo>
```


The facts handed to an engine's doctor (`DoctorAsync`), once per target in the process `e2e doctor`
runs in. Upstream's `EngineDoctorInfo`; its `signal` is the [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) of the call.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineDoctorInfo&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Properties

<a id="E2E_Engine_EngineDoctorInfo_App_"></a>
<a id="E2E_Engine_EngineDoctorInfo_App"></a>

### App


```csharp
public required EngineAppInfo App { get; init; }
```


The app the target declares.

**Value:** [EngineAppInfo](/api/E2E.Engine/EngineAppInfo/)

<a id="E2E_Engine_EngineDoctorInfo_Env_"></a>
<a id="E2E_Engine_EngineDoctorInfo_Env"></a>

### Env


```csharp
public required IReadOnlyDictionary<string, string?> Env { get; init; }
```


The environment a run would start with.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_Engine_EngineDoctorInfo_Headed_"></a>
<a id="E2E_Engine_EngineDoctorInfo_Headed"></a>

### Headed


```csharp
public required bool Headed { get; init; }
```


Whether the run would show the surface (`--headed`).

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_Engine_EngineDoctorInfo_Log_"></a>
<a id="E2E_Engine_EngineDoctorInfo_Log"></a>

### Log


```csharp
public required Action<string> Log { get; init; }
```


Reports one line of progress; `e2e doctor` prints it to stderr.

**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

<a id="E2E_Engine_EngineDoctorInfo_ProjectRoot_"></a>
<a id="E2E_Engine_EngineDoctorInfo_ProjectRoot"></a>

### ProjectRoot


```csharp
public required string ProjectRoot { get; init; }
```


The directory relative paths in the config resolve against, as [ProjectRoot](/api/E2E.Engine/EnginePrepareInfo/#E2E_Engine_EnginePrepareInfo_ProjectRoot).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineDoctorInfo_TargetName_"></a>
<a id="E2E_Engine_EngineDoctorInfo_TargetName"></a>

### TargetName


```csharp
public required string TargetName { get; init; }
```


The target being checked.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
