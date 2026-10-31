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
`retries`, `cache`, and `secrets`. Unknown keys fail with
`INVALID_CONFIG`. `E2ETest` finds and applies this file; a fixture can
still override each value. There is no default model.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L19)

## Fields

<a id="E2E_E2EConfig_FileName"></a>

### FileName


```csharp
public const string FileName = "e2e.config.json"
```


The file name `String%5b%5d)` looks for.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L22)

## Properties

<a id="E2E_E2EConfig_ActionTimeout_"></a>
<a id="E2E_E2EConfig_ActionTimeout"></a>

### ActionTimeout


```csharp
public TimeSpan ActionTimeout { get; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L106)

<a id="E2E_E2EConfig_Agent_"></a>
<a id="E2E_E2EConfig_Agent"></a>

### Agent


```csharp
public AgentConfig Agent { get; }
```


The `default` agent. It always exists; its model may be null.

**Value:** [AgentConfig](/api/E2E/AgentConfig/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L119)

<a id="E2E_E2EConfig_Agents_"></a>
<a id="E2E_E2EConfig_Agents"></a>

### Agents


```csharp
public IReadOnlyDictionary<string, AgentConfig> Agents { get; }
```


**Value:** [IReadOnlyDictionary&lt;string, AgentConfig&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L116)

<a id="E2E_E2EConfig_AssertionTimeout_"></a>
<a id="E2E_E2EConfig_AssertionTimeout"></a>

### AssertionTimeout


```csharp
public TimeSpan AssertionTimeout { get; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L108)

<a id="E2E_E2EConfig_Cache_"></a>
<a id="E2E_E2EConfig_Cache"></a>

### Cache


```csharp
public CacheConfig Cache { get; }
```


**Value:** [CacheConfig](/api/E2E/CacheConfig/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L121)

<a id="E2E_E2EConfig_Ci_"></a>
<a id="E2E_E2EConfig_Ci"></a>

### Ci


```csharp
public bool Ci { get; }
```


True when the `CI` environment variable is set to a true value.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L93)

<a id="E2E_E2EConfig_CleanupTimeout_"></a>
<a id="E2E_E2EConfig_CleanupTimeout"></a>

### CleanupTimeout


```csharp
public TimeSpan CleanupTimeout { get; }
```


Engine shutdown timeout. Upstream `cleanupTimeout`, 30 s by default.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L111)

<a id="E2E_E2EConfig_ConfigPath_"></a>
<a id="E2E_E2EConfig_ConfigPath"></a>

### ConfigPath


```csharp
public string? ConfigPath { get; }
```


The file this config was read from, or null for the defaults.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L87)

<a id="E2E_E2EConfig_LaunchTimeout_"></a>
<a id="E2E_E2EConfig_LaunchTimeout"></a>

### LaunchTimeout


```csharp
public TimeSpan LaunchTimeout { get; }
```


Engine start timeout. Upstream `launchTimeout`, 60 s by default.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L104)

<a id="E2E_E2EConfig_ProjectRoot_"></a>
<a id="E2E_E2EConfig_ProjectRoot"></a>

### ProjectRoot


```csharp
public string ProjectRoot { get; }
```


The config file's directory. Relative paths such as `cache.dir` resolve against it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L90)

<a id="E2E_E2EConfig_Retries_"></a>
<a id="E2E_E2EConfig_Retries"></a>

### Retries


```csharp
public int Retries { get; }
```


0 to 10. Defaults to 1 in CI and 0 elsewhere. NUnit retries still come from `[Retry]`.

**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L114)

<a id="E2E_E2EConfig_Secrets_"></a>
<a id="E2E_E2EConfig_Secrets"></a>

### Secrets


```csharp
public Secrets Secrets { get; }
```


**Value:** [Secrets](/api/E2E/Secrets/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L123)

<a id="E2E_E2EConfig_Target_"></a>
<a id="E2E_E2EConfig_Target"></a>

### Target


```csharp
public TargetConfig Target { get; }
```


The target a test runs on. The port runs one target.

**Value:** [TargetConfig](/api/E2E/TargetConfig/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L98)

<a id="E2E_E2EConfig_Targets_"></a>
<a id="E2E_E2EConfig_Targets"></a>

### Targets


```csharp
public IReadOnlyList<TargetConfig> Targets { get; }
```


**Value:** [IReadOnlyList&lt;TargetConfig&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L95)

<a id="E2E_E2EConfig_Timeout_"></a>
<a id="E2E_E2EConfig_Timeout"></a>

### Timeout


```csharp
public TimeSpan Timeout { get; }
```


Whole-test timeout. Upstream `timeout`, 120 s by default.

**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L101)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L152)

<a id="E2E_E2EConfig_Discover_"></a>
<a id="E2E_E2EConfig_Discover_System_Func_System_String_System_String__"></a>

### `Discover(Func<string, string?>?)`


```csharp
public static E2EConfig Discover(Func<string, string?>? environment = null)
```


Loads the nearest [FileName](#E2E_E2EConfig_FileName) above the test assembly directory, then
above the working directory. Without one, returns `String%7d)` for the working directory.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `environment` | [Func&lt;string, string&gt;](https://learn.microsoft.com/dotnet/api/system.func-2) |  |

**Returns:** [E2EConfig](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L195)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L163)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L202)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L126)

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

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L144)
