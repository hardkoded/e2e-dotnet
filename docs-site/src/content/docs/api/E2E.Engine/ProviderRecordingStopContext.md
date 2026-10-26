---
title: "ProviderRecordingStopContext"
description: "Handed to CancellationToken), once per recording."
editUrl: false
prev: false
next: false
sidebar:
  label: "ProviderRecordingStopContext"
  hidden: true
---

<a id="E2E_Engine_ProviderRecordingStopContext"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record ProviderRecordingStopContext : IEquatable<ProviderRecordingStopContext>
```


Handed to `CancellationToken)`, once per recording.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ProviderRecordingStopContext&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L24)

## Properties

<a id="E2E_Engine_ProviderRecordingStopContext_Dir_"></a>
<a id="E2E_Engine_ProviderRecordingStopContext_Dir"></a>

### Dir


```csharp
public required string Dir { get; init; }
```


Absolute directory, already created, a recording written as a file goes into.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/ProviderRecording.cs#L27)
