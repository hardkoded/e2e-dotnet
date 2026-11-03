---
title: "E2EConfig"
description: "The resolved e2e.config.json. The JSON shape follows upstream e2e.config.ts: targets[].app.url, named agents, top-level timeouts in milliseconds, retries, ca..."
editUrl: false
prev: false
next: false
sidebar:
  label: "E2EConfig"
  hidden: true
---

<a id="E2E_E2EConfig"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class E2EConfig
```


The resolved `e2e.config.json`. The JSON shape follows upstream `e2e.config.ts`:
`targets[].app.url`, named `agents`, top-level timeouts in milliseconds,
`retries`, `cache`, `secrets`, and `reporters` (type names, .NET-only). Unknown keys fail with
`INVALID_CONFIG`. `E2ETest` finds and applies this file; a fixture can
still override each value. There is no default model.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs)

## Fields

<a id="E2E_E2EConfig_FileName"></a>

### FileName


```csharp
public const string FileName = "e2e.config.json"
```


The file name `Find` looks for.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

## Properties

<a id="E2E_E2EConfig_ActionTimeout_"></a>
<a id="E2E_E2EConfig_ActionTimeout"></a>

### ActionTimeout


```csharp
public TimeSpan ActionTimeout { get; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_E2EConfig_Agent_"></a>
<a id="E2E_E2EConfig_Agent"></a>

### Agent


```csharp
public AgentConfig Agent { get; }
```


The `default` agent. It always exists; its model may be null.

**Value:** [AgentConfig](/api/E2E/AgentConfig/)

<a id="E2E_E2EConfig_Agents_"></a>
<a id="E2E_E2EConfig_Agents"></a>

### Agents


```csharp
public IReadOnlyDictionary<string, AgentConfig> Agents { get; }
```


**Value:** [IReadOnlyDictionary&lt;string, AgentConfig&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_E2EConfig_ArtifactStore_"></a>
<a id="E2E_E2EConfig_ArtifactStore"></a>

### ArtifactStore


```csharp
public IArtifactStore? ArtifactStore { get; }
```


Where artifacts go, over the files under the results directory (upstream's `artifacts.store`, `ResolvedConfig.artifactStore`).
None by default. A store is code, so the config file cannot name one: [WithArtifactStore(IArtifactStore)](#E2E_E2EConfig_WithArtifactStore_E2E_IArtifactStore_) sets it.

**Value:** [IArtifactStore](/api/E2E/IArtifactStore/)

<a id="E2E_E2EConfig_AssertionTimeout_"></a>
<a id="E2E_E2EConfig_AssertionTimeout"></a>

### AssertionTimeout


```csharp
public TimeSpan AssertionTimeout { get; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_E2EConfig_Cache_"></a>
<a id="E2E_E2EConfig_Cache"></a>

### Cache


```csharp
public CacheConfig Cache { get; }
```


**Value:** [CacheConfig](/api/E2E/CacheConfig/)

<a id="E2E_E2EConfig_Ci_"></a>
<a id="E2E_E2EConfig_Ci"></a>

### Ci


```csharp
public bool Ci { get; }
```


True when the `CI` environment variable is set to a true value.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_E2EConfig_CleanupTimeout_"></a>
<a id="E2E_E2EConfig_CleanupTimeout"></a>

### CleanupTimeout


```csharp
public TimeSpan CleanupTimeout { get; }
```


Engine shutdown timeout. Upstream `cleanupTimeout`, 30 s by default.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_E2EConfig_ConfigPath_"></a>
<a id="E2E_E2EConfig_ConfigPath"></a>

### ConfigPath


```csharp
public string? ConfigPath { get; }
```


The file this config was read from, or null for the defaults.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_E2EConfig_Credentials_"></a>
<a id="E2E_E2EConfig_Credentials"></a>

### Credentials


```csharp
public IReadOnlyDictionary<string, ResolvedCredential> Credentials { get; }
```


The `credentials` entries by name, each with its password as the secret `<name>.password` (upstream's `ResolvedConfig.credentials`).

**Value:** [IReadOnlyDictionary&lt;string, ResolvedCredential&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

<a id="E2E_E2EConfig_LaunchTimeout_"></a>
<a id="E2E_E2EConfig_LaunchTimeout"></a>

### LaunchTimeout


```csharp
public TimeSpan LaunchTimeout { get; }
```


Engine start timeout. Upstream `launchTimeout`, 60 s by default.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

<a id="E2E_E2EConfig_Output_"></a>
<a id="E2E_E2EConfig_Output"></a>

### Output


```csharp
public string Output { get; }
```


The absolute directory the runs write their results to: `output`, else `<ProjectRoot>/.e2e`. Results go to
`results/` under it, the session stores to `sessions/`, and an `e2e mcp` recording to `videos/`. Upstream's
`ResolvedConfig.output`. It is checked when read, not when the config loads, so `--output` can replace a configured value
that is refused (upstream checks only the one that is used). Throws `INVALID_CONFIG` for a directory the run cannot own.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_E2EConfig_ProjectRoot_"></a>
<a id="E2E_E2EConfig_ProjectRoot"></a>

### ProjectRoot


```csharp
public string ProjectRoot { get; }
```


The config file's directory. Relative paths such as `cache.dir` resolve against it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_E2EConfig_Retries_"></a>
<a id="E2E_E2EConfig_Retries"></a>

### Retries


```csharp
public int Retries { get; }
```


0 to 10. Defaults to 1 in CI and 0 elsewhere. NUnit retries still come from `[Retry]`.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

<a id="E2E_E2EConfig_Secrets_"></a>
<a id="E2E_E2EConfig_Secrets"></a>

### Secrets


```csharp
public Secrets Secrets { get; }
```


**Value:** [Secrets](/api/E2E/Secrets/)

<a id="E2E_E2EConfig_Target_"></a>
<a id="E2E_E2EConfig_Target"></a>

### Target


```csharp
public TargetConfig Target { get; }
```


The only target the config declares. .NET-only: a fixture, a host, or a command with no target of its own reads it. With several
targets it throws `TARGET_REQUIRED` (upstream's `selectTarget`): name one from [Targets](#E2E_E2EConfig_Targets).

**Value:** [TargetConfig](/api/E2E/TargetConfig/)

<a id="E2E_E2EConfig_Targets_"></a>
<a id="E2E_E2EConfig_Targets"></a>

### Targets


```csharp
public IReadOnlyList<TargetConfig> Targets { get; }
```


**Value:** [IReadOnlyList&lt;TargetConfig&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

<a id="E2E_E2EConfig_TestAssembly_"></a>
<a id="E2E_E2EConfig_TestAssembly"></a>

### TestAssembly


```csharp
public string? TestAssembly { get; }
```


The absolute path of the test assembly `e2e mcp` runs a test from (`open_session {test}`), or null. .NET-only: upstream
collects tests from its `tests` globs. The config's `testAssembly` is relative to the config's directory.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_E2EConfig_Timeout_"></a>
<a id="E2E_E2EConfig_Timeout"></a>

### Timeout


```csharp
public TimeSpan Timeout { get; }
```


Whole-test timeout. Upstream `timeout`, 120 s by default.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

## Methods

<a id="E2E_E2EConfig_Default_"></a>
<a id="E2E_E2EConfig_Default_System_String_System_Func_System_String_System_String__"></a>

### `Default(string, Func<string, string?>?)`


```csharp
public static E2EConfig Default(string projectRoot, Func<string, string?>? environment = null)
```


The config with no file: upstream defaults, a `web` target with no URL, and no model.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `projectRoot` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `environment` | [Func&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |

**Returns:** [E2EConfig](./)

<a id="E2E_E2EConfig_Discover_"></a>
<a id="E2E_E2EConfig_Discover_System_Func_System_String_System_String__"></a>

### `Discover(Func<string, string?>?)`


```csharp
public static E2EConfig Discover(Func<string, string?>? environment = null)
```


Loads the nearest [FileName](#E2E_E2EConfig_FileName) above the test assembly directory, then
above the working directory. Without one, returns `Default` for the working directory.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `environment` | [Func&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |

**Returns:** [E2EConfig](./)

<a id="E2E_E2EConfig_Find_"></a>
<a id="E2E_E2EConfig_Find_System_String___"></a>

### Find(params string[])


```csharp
public static string? Find(params string[] startDirectories)
```


The nearest [FileName](#E2E_E2EConfig_FileName) in each start directory or one of its parents,
tried in order, or null. A directory with a `.git` entry is the repository root: the search
looks for the file there and goes no higher.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `startDirectories` | [string\[\]](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_E2EConfig_IsCi_"></a>
<a id="E2E_E2EConfig_IsCi_System_Func_System_String_System_String__"></a>

### `IsCi(Func<string, string?>?)`


```csharp
public static bool IsCi(Func<string, string?>? environment = null)
```


True when `CI` is set to anything but empty, `0`, or `false`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `environment` | [Func&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |

**Returns:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a id="E2E_E2EConfig_Load_"></a>
<a id="E2E_E2EConfig_Load_System_String_System_Func_System_String_System_String__"></a>

### `Load(string, Func<string, string?>?)`


```csharp
public static E2EConfig Load(string path, Func<string, string?>? environment = null)
```


Reads and validates a config file. Throws [ConfigurationException](/api/E2E/ConfigurationException/) with `INVALID_CONFIG`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `path` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `environment` | [Func&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |

**Returns:** [E2EConfig](./)

<a id="E2E_E2EConfig_Parse_"></a>
<a id="E2E_E2EConfig_Parse_System_String_System_String_System_Func_System_String_System_String__"></a>

### `Parse(string, string, Func<string, string?>?)`


```csharp
public static E2EConfig Parse(string json, string projectRoot, Func<string, string?>? environment = null)
```


Validates config JSON. &lt;code class="paramref"&gt;projectRoot&lt;/code&gt; anchors relative paths.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `json` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `projectRoot` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `environment` | [Func&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |

**Returns:** [E2EConfig](./)

<a id="E2E_E2EConfig_WithArtifactStore_"></a>
<a id="E2E_E2EConfig_WithArtifactStore_E2E_IArtifactStore_"></a>

### WithArtifactStore(IArtifactStore)


```csharp
public E2EConfig WithArtifactStore(IArtifactStore store)
```


A copy of the config whose artifacts go to &lt;code class="paramref"&gt;store&lt;/code&gt; as well (upstream's `artifacts.store`). .NET-only: the config
file cannot name a store, so code sets it.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `store` | [IArtifactStore](/api/E2E/IArtifactStore/) |  |

**Returns:** [E2EConfig](./)

<a id="E2E_E2EConfig_WithCacheStore_"></a>
<a id="E2E_E2EConfig_WithCacheStore_E2E_IStepCache_"></a>

### WithCacheStore(IStepCache)


```csharp
public E2EConfig WithCacheStore(IStepCache store)
```


A copy of the config whose replay cache is &lt;code class="paramref"&gt;store&lt;/code&gt; (upstream's `cache.store`), over the file store at
`Cache.Directory`. .NET-only: the config file cannot name a store, so code sets it. Like upstream, a store ends the
`E2E_CACHE_DIR` override (a store has no directory), and CI does not turn an unset mode into [ReadOnly](/api/E2E/CacheMode/#E2E_CacheMode_ReadOnly):
the store states its own trust.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `store` | [IStepCache](/api/E2E/IStepCache/) |  |

**Returns:** [E2EConfig](./)
