---
title: "SessionState"
description: "What a session's SaveStateAsync captures (the engine's cookies, storage, or app data), held in memory. Upstream's SessionState (core/state.ts). It leaves the..."
editUrl: false
prev: false
next: false
sidebar:
  label: "SessionState"
  hidden: true
---

<a id="E2E_SessionState"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class SessionState
```


What a session's `SaveStateAsync` captures (the engine's cookies, storage, or app data), held in memory. Upstream's `SessionState`
(`core/state.ts`). It leaves the process only encrypted, under a key the caller holds, as a `session-state-1` document; there is no
plaintext export. Pass it to `NewSessionAsync` to start a session from it, on a host of the same engine, platform, and app.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Core/SessionState.cs)

## Properties

<a id="E2E_SessionState_Engine_"></a>
<a id="E2E_SessionState_Engine"></a>

### Engine


```csharp
public string Engine { get; }
```


The engine that captured it, by name.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_SessionState_Platform_"></a>
<a id="E2E_SessionState_Platform"></a>

### Platform


```csharp
public string Platform { get; }
```


The platform it was captured on.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

<a id="E2E_SessionState_ExportAsync_"></a>
<a id="E2E_SessionState_ExportAsync_E2E_SessionStateKeyOptions_"></a>

### ExportAsync(SessionStateKeyOptions)


```csharp
public Task<byte[]> ExportAsync(SessionStateKeyOptions options)
```


Encrypts the state with the key (AES-256-GCM, 32 bytes) into a `session-state-1` document, as UTF-8 bytes. The document names the
engine, the platform, a digest of the app, and the names of the secrets in the clear, authenticated; everything the state holds,
secret values included, is in the ciphertext.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `options` | [SessionStateKeyOptions](/api/E2E/SessionStateKeyOptions/) |  |

**Returns:** [Task&lt;byte\[\]&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)

<a id="E2E_SessionState_ImportAsync_"></a>
<a id="E2E_SessionState_ImportAsync_System_Byte___E2E_SessionStateKeyOptions_"></a>

### ImportAsync(byte[], SessionStateKeyOptions)


```csharp
public static Task<SessionState> ImportAsync(byte[] data, SessionStateKeyOptions options)
```


Decrypts a document [ExportAsync(SessionStateKeyOptions)](#E2E_SessionState_ExportAsync_E2E_SessionStateKeyOptions_) wrote. A document that is not `session-state-1`, or that fails authentication under
the key (a wrong key, an edited byte), is `SESSION_INVALID`; an expired one is `SESSION_EXPIRED`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `data` | [byte\[\]](https://learn.microsoft.com/dotnet/api/system.byte) |  |
| `options` | [SessionStateKeyOptions](/api/E2E/SessionStateKeyOptions/) |  |

**Returns:** [Task&lt;SessionState&gt;](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
