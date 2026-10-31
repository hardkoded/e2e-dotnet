---
title: "CommandConfig"
description: "The command a target's app.command starts the app with (upstream's CommandConfig). It is a program and its arguments, never a shell line: the executable is s..."
editUrl: false
prev: false
next: false
sidebar:
  label: "CommandConfig"
  hidden: true
---

<a id="E2E_CommandConfig"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class CommandConfig
```


The command a target's `app.command` starts the app with (upstream's `CommandConfig`). It is a program and its arguments,
never a shell line: the executable is started directly and each argument is passed as it is.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/Command.cs#L18)

## Properties

<a id="E2E_CommandConfig_Args_"></a>
<a id="E2E_CommandConfig_Args"></a>

### Args


```csharp
public IReadOnlyList<string>? Args { get; init; }
```


Its arguments, passed one by one. `{port}` is replaced by the port the app is served on.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/Command.cs#L24)

<a id="E2E_CommandConfig_Cwd_"></a>
<a id="E2E_CommandConfig_Cwd"></a>

### Cwd


```csharp
public string? Cwd { get; init; }
```


The working directory, relative to the config's directory. Unset, that directory.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/Command.cs#L27)

<a id="E2E_CommandConfig_Env_"></a>
<a id="E2E_CommandConfig_Env"></a>

### Env


```csharp
public IReadOnlyDictionary<string, string>? Env { get; init; }
```


Variables the process gets, on top of [InheritEnv](#E2E_CommandConfig_InheritEnv). `{port}` in a value is replaced.

**Value:** [IReadOnlyDictionary&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/Command.cs#L30)

<a id="E2E_CommandConfig_Executable_"></a>
<a id="E2E_CommandConfig_Executable"></a>

### Executable


```csharp
public required string Executable { get; init; }
```


The program to start. Required.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/Command.cs#L21)

<a id="E2E_CommandConfig_InheritEnv_"></a>
<a id="E2E_CommandConfig_InheritEnv"></a>

### InheritEnv


```csharp
public IReadOnlyList<string>? InheritEnv { get; init; }
```


Variable names, or prefixes ending in `*`, copied from the environment of the run. The process gets only these,
[Env](#E2E_CommandConfig_Env), and `PATH`, `HOME`, `TMPDIR`, `TMP`, `TEMP`, `SystemRoot`, and `COMSPEC`.

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/Command.cs#L36)

<a id="E2E_CommandConfig_Log_"></a>
<a id="E2E_CommandConfig_Log"></a>

### Log


```csharp
public string? Log { get; init; }
```


A file inside the config's directory that takes the output of the process, appended to across runs.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/Command.cs#L45)

<a id="E2E_CommandConfig_ReuseExisting_"></a>
<a id="E2E_CommandConfig_ReuseExisting"></a>

### ReuseExisting


```csharp
public bool? ReuseExisting { get; init; }
```


Attach to an app that already answers at the readiness URL instead of failing. Ignored in CI.

**Value:** [bool?](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/Command.cs#L48)

<a id="E2E_CommandConfig_ShutdownTimeout_"></a>
<a id="E2E_CommandConfig_ShutdownTimeout"></a>

### ShutdownTimeout


```csharp
public TimeSpan? ShutdownTimeout { get; init; }
```


How long the app has to stop before it is killed. 10 seconds when unset.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/Command.cs#L42)

<a id="E2E_CommandConfig_StartupTimeout_"></a>
<a id="E2E_CommandConfig_StartupTimeout"></a>

### StartupTimeout


```csharp
public TimeSpan? StartupTimeout { get; init; }
```


How long the app has to become ready. 60 seconds when unset.

**Value:** [TimeSpan?](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/Command.cs#L39)
