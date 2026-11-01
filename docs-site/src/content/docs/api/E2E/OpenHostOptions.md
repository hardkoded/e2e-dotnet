---
title: "OpenHostOptions"
description: "What OpenAsync is given. Upstream's OpenHostOptions (run/open-session.ts)."
editUrl: false
prev: false
next: false
sidebar:
  label: "OpenHostOptions"
  hidden: true
---

<a id="E2E_OpenHostOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record OpenHostOptions : IEquatable<OpenHostOptions>
```


What `OpenAsync` is given. Upstream's `OpenHostOptions` (`run/open-session.ts`).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;OpenHostOptions&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L11)

## Properties

<a id="E2E_OpenHostOptions_Config_"></a>
<a id="E2E_OpenHostOptions_Config"></a>

### Config


```csharp
public E2EConfig? Config { get; init; }
```


The config as a value, which the host uses instead of a file; its [ProjectRoot](/api/E2E/E2EConfig/#E2E_E2EConfig_ProjectRoot) is the project root. Upstream's
`config` when it is an object. A value cannot hold a live engine here (see [EngineFactory](#E2E_OpenHostOptions_EngineFactory)).

**Value:** [E2EConfig](/api/E2E/E2EConfig/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L20)

<a id="E2E_OpenHostOptions_ConfigPath_"></a>
<a id="E2E_OpenHostOptions_ConfigPath"></a>

### ConfigPath


```csharp
public string? ConfigPath { get; init; }
```


The config file, as a path relative to [Cwd](#E2E_OpenHostOptions_Cwd); default the nearest `e2e.config.json`. It is loaded fresh per host. Upstream's
`config` when it is a string, kept apart from [Config](#E2E_OpenHostOptions_Config) because C# has no union of the two.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L26)

<a id="E2E_OpenHostOptions_Cwd_"></a>
<a id="E2E_OpenHostOptions_Cwd"></a>

### Cwd


```csharp
public string? Cwd { get; init; }
```


Where the config file is searched for upward; default the current directory. Upstream's `cwd`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L14)

<a id="E2E_OpenHostOptions_EngineFactory_"></a>
<a id="E2E_OpenHostOptions_EngineFactory"></a>

### EngineFactory


```csharp
public Func<TargetConfig, IEngine>? EngineFactory { get; init; }
```


.NET-only: builds the engine of the selected target. Upstream's config holds a live engine handle; `e2e.config.json` names a platform
instead, so the host would build a [WebEngine](/api/E2E.Engine/WebEngine/) for a `web` target and fail for the others. A device or Windows target, or a
test engine, comes from here, and a factory builds a fresh engine for each host.

**Value:** [Func&lt;TargetConfig, IEngine&gt;](https://learn.microsoft.com/dotnet/api/system.func-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L45)

<a id="E2E_OpenHostOptions_Env_"></a>
<a id="E2E_OpenHostOptions_Env"></a>

### Env


```csharp
public IReadOnlyDictionary<string, string?>? Env { get; init; }
```


The variables the config and the engine read; null is the process environment. Upstream's `env`.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L32)

<a id="E2E_OpenHostOptions_Headed_"></a>
<a id="E2E_OpenHostOptions_Headed"></a>

### Headed


```csharp
public bool Headed { get; init; }
```


Whether a web target opens a visible window. Upstream's `headed`.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L35)

<a id="E2E_OpenHostOptions_OnNotice_"></a>
<a id="E2E_OpenHostOptions_OnNotice"></a>

### OnNotice


```csharp
public Action<string, string>? OnNotice { get; init; }
```


Progress outside steps: provisioning, leases, and app processes, as the target's name and a line. Must not throw. Upstream's `onNotice`.

**Value:** [Action&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.action-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L38)

<a id="E2E_OpenHostOptions_Target_"></a>
<a id="E2E_OpenHostOptions_Target"></a>

### Target


```csharp
public string? Target { get; init; }
```


The target to open. It may be omitted only when the config declares one target. Upstream's `target`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Run/OpenSession.cs#L29)
