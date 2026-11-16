---
title: "SessionStateKeyOptions"
description: "The options of ExportAsync(SessionStateKeyOptions) and ImportAsync: the AES-256-GCM key, 32 bytes the caller keeps. Upstream's SessionStateKeyOptions."
editUrl: false
prev: false
next: false
sidebar:
  label: "SessionStateKeyOptions"
  hidden: true
---

<a id="E2E_SessionStateKeyOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record SessionStateKeyOptions : IEquatable<SessionStateKeyOptions>
```


The options of [ExportAsync(SessionStateKeyOptions)](/api/E2E/SessionState/#E2E_SessionState_ExportAsync_E2E_SessionStateKeyOptions_) and `ImportAsync`: the AES-256-GCM key, 32 bytes the caller keeps. Upstream's `SessionStateKeyOptions`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;SessionStateKeyOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/SessionState.cs)

## Properties

<a id="E2E_SessionStateKeyOptions_Key_"></a>
<a id="E2E_SessionStateKeyOptions_Key"></a>

### Key


```csharp
public required byte[] Key { get; init; }
```


The 32-byte AES-256-GCM key.

**Value:** [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte)
