---
title: "ProviderRecordContext"
description: "Handed to a provider's RecordAsync, once per attempt that records video."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProviderRecordContext"
  hidden: true
---

<a id="E2E_Engine_ProviderRecordContext"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record ProviderRecordContext : IEquatable<ProviderRecordContext>
```


Handed to a provider's `RecordAsync`, once per attempt that records video.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ProviderRecordContext&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L10)

## Properties

<a id="E2E_Engine_ProviderRecordContext_AttemptId_"></a>
<a id="E2E_Engine_ProviderRecordContext_AttemptId"></a>

### AttemptId


```csharp
public required string AttemptId { get; init; }
```


The attempt the recording covers.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L17)

<a id="E2E_Engine_ProviderRecordContext_Env_"></a>
<a id="E2E_Engine_ProviderRecordContext_Env"></a>

### Env


```csharp
public required IReadOnlyDictionary<string, string?> Env { get; init; }
```


The run's environment, the same the provider's acquire saw.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L20)

<a id="E2E_Engine_ProviderRecordContext_RunId_"></a>
<a id="E2E_Engine_ProviderRecordContext_RunId"></a>

### RunId


```csharp
public required string RunId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L12)

<a id="E2E_Engine_ProviderRecordContext_TargetName_"></a>
<a id="E2E_Engine_ProviderRecordContext_TargetName"></a>

### TargetName


```csharp
public required string TargetName { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L14)
