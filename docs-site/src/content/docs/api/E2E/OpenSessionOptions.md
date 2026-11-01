---
title: "OpenSessionOptions"
description: "What OpenSessionAsync is given. Upstream's OpenSessionOptions (run/open-session.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "OpenSessionOptions"
  hidden: true
---

<a id="E2E_OpenSessionOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record OpenSessionOptions : IEquatable<OpenSessionOptions>
```


What `OpenSessionAsync` is given. Upstream's `OpenSessionOptions` (`run/open-session.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;OpenSessionOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L49)

## Properties

<a id="E2E_OpenSessionOptions_Agent_"></a>
<a id="E2E_OpenSessionOptions_Agent"></a>

### Agent


```csharp
public string? Agent { get; init; }
```


The configured agent calls use when they name none. Upstream's `agent`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L62)

<a id="E2E_OpenSessionOptions_Name_"></a>
<a id="E2E_OpenSessionOptions_Name"></a>

### Name


```csharp
public required string Name { get; init; }
```


The test's title, treated as untrusted text and never a raw path component. Upstream's `name`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L52)

<a id="E2E_OpenSessionOptions_OnStep_"></a>
<a id="E2E_OpenSessionOptions_OnStep"></a>

### OnStep


```csharp
public Action<StepProgress>? OnStep { get; init; }
```


The step progress feed reporters receive. Must not block or throw. Upstream's `onStep`.

**Value:** [Action&lt;StepProgress&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L65)

<a id="E2E_OpenSessionOptions_Timeout_"></a>
<a id="E2E_OpenSessionOptions_Timeout"></a>

### Timeout


```csharp
public TimeSpan? Timeout { get; init; }
```


The deadline of the session's operations, counted from when it opens; default the config's `timeout`. Positive and finite.
Expiry cuts running operations and fails the session as timed out; it does not bound opening (use the cancellation token) and does not
close the session. Upstream's `timeout`.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L59)
