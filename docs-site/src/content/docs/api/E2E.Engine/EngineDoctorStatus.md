---
title: "EngineDoctorStatus"
description: "How one finding of an engine's doctor went. Upstream's EngineDoctorCheck.status, a union of four strings. .NET-only name: a C# union has to be a type."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineDoctorStatus"
  hidden: true
---

<a id="E2E_Engine_EngineDoctorStatus"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public enum EngineDoctorStatus
```


How one finding of an engine's doctor went. Upstream's `EngineDoctorCheck.status`, a union of four strings. .NET-only name: a C#
union has to be a type.

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Fields

<a id="E2E_Engine_EngineDoctorStatus_Fail"></a>

### Fail


```csharp
Fail = 2
```


Something that stops a run.

**Value:** [EngineDoctorStatus](./)

<a id="E2E_Engine_EngineDoctorStatus_Pass"></a>

### Pass


```csharp
Pass = 0
```


What was checked is there.

**Value:** [EngineDoctorStatus](./)

<a id="E2E_Engine_EngineDoctorStatus_Skip"></a>

### Skip


```csharp
Skip = 3
```


A check that does not apply to this target.

**Value:** [EngineDoctorStatus](./)

<a id="E2E_Engine_EngineDoctorStatus_Warn"></a>

### Warn


```csharp
Warn = 1
```


Something a run handles on its own, slower: a first-run download.

**Value:** [EngineDoctorStatus](./)
