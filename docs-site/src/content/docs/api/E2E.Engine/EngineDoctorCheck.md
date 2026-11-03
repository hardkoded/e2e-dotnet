---
title: "EngineDoctorCheck"
description: "One finding of DoctorAsync. Upstream's EngineDoctorCheck."
editUrl: false
prev: false
next: false
sidebar:
  label: "EngineDoctorCheck"
  hidden: true
---

<a id="E2E_Engine_EngineDoctorCheck"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record EngineDoctorCheck : IEquatable<EngineDoctorCheck>
```


One finding of `DoctorAsync`. Upstream's `EngineDoctorCheck`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;EngineDoctorCheck&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/IEngine.cs)

## Constructors

<a id="E2E_Engine_EngineDoctorCheck__ctor_"></a>
<a id="E2E_Engine_EngineDoctorCheck__ctor_System_String_E2E_Engine_EngineDoctorStatus_System_String_"></a>

### EngineDoctorCheck(string, EngineDoctorStatus, string?)


```csharp
public EngineDoctorCheck(string Id, EngineDoctorStatus Status, string? Message = null)
```


One finding of `DoctorAsync`. Upstream's `EngineDoctorCheck`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Id` | [string](https://learn.microsoft.com/dotnet/api/system.string) | What was checked, in kebab case: `browser`, `toolchain`. `node`, `config`, `runtime`, `engine`, and `model` are `e2e doctor`'s own. |
| `Status` | [EngineDoctorStatus](/api/E2E.Engine/EngineDoctorStatus/) | How the check went. |
| `Message` | [string](https://learn.microsoft.com/dotnet/api/system.string) | One line: what was found and, unless it passed, what to do. |

## Properties

<a id="E2E_Engine_EngineDoctorCheck_Id_"></a>
<a id="E2E_Engine_EngineDoctorCheck_Id"></a>

### Id


```csharp
public string Id { get; init; }
```


What was checked, in kebab case: `browser`, `toolchain`. `node`, `config`, `runtime`, `engine`, and `model` are `e2e doctor`'s own.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineDoctorCheck_Message_"></a>
<a id="E2E_Engine_EngineDoctorCheck_Message"></a>

### Message


```csharp
public string? Message { get; init; }
```


One line: what was found and, unless it passed, what to do.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_Engine_EngineDoctorCheck_Status_"></a>
<a id="E2E_Engine_EngineDoctorCheck_Status"></a>

### Status


```csharp
public EngineDoctorStatus Status { get; init; }
```


How the check went.

**Value:** [EngineDoctorStatus](/api/E2E.Engine/EngineDoctorStatus/)
