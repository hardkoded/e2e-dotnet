---
title: "BrowserReleaseContext"
description: "Handed to release, once per lease."
editUrl: false
prev: false
next: false
sidebar:
  label: "BrowserReleaseContext"
  hidden: true
---

<a id="E2E_Engine_BrowserReleaseContext"></a>

Namespace: [E2E.Engine](/api/E2E.Engine/)


```csharp
public sealed record BrowserReleaseContext : IEquatable<BrowserReleaseContext>
```


Handed to release, once per lease.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;BrowserReleaseContext&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L92)

## Properties

<a id="E2E_Engine_BrowserReleaseContext_Env_"></a>
<a id="E2E_Engine_BrowserReleaseContext_Env"></a>

### Env


```csharp
public required IReadOnlyDictionary<string, string?> Env { get; init; }
```


The run's environment, the same acquire saw.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L99)

<a id="E2E_Engine_BrowserReleaseContext_Log_"></a>
<a id="E2E_Engine_BrowserReleaseContext_Log"></a>

### Log


```csharp
public required Action<string> Log { get; init; }
```


**Value:** [Action&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.action-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L101)

<a id="E2E_Engine_BrowserReleaseContext_RunId_"></a>
<a id="E2E_Engine_BrowserReleaseContext_RunId"></a>

### RunId


```csharp
public required string RunId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L94)

<a id="E2E_Engine_BrowserReleaseContext_TargetName_"></a>
<a id="E2E_Engine_BrowserReleaseContext_TargetName"></a>

### TargetName


```csharp
public required string TargetName { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Engine/BrowserProvider.cs#L96)
