---
title: "AgentConfig"
description: "One entry of agents. Upstream model and judge are AI SDK instances; here they are model ids for Provider, which share BaseUrl and ApiKeyEnv. Each entry start..."
editUrl: false
prev: false
next: false
sidebar:
  label: "AgentConfig"
  hidden: true
---

<a id="E2E_AgentConfig"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class AgentConfig
```


One entry of `agents`. Upstream `model` and `judge` are AI SDK instances; here they are model ids
for [Provider](#E2E_AgentConfig_Provider), which share [BaseUrl](#E2E_AgentConfig_BaseUrl) and [ApiKeyEnv](#E2E_AgentConfig_ApiKeyEnv).
Each entry starts from the defaults; none inherits another's values.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L741)

## Fields

<a id="E2E_AgentConfig_DefaultProvider"></a>

### DefaultProvider


```csharp
public const string DefaultProvider = "openai"
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L743)

## Properties

<a id="E2E_AgentConfig_ApiKeyEnv_"></a>
<a id="E2E_AgentConfig_ApiKeyEnv"></a>

### ApiKeyEnv


```csharp
public string? ApiKeyEnv { get; init; }
```


The environment variable holding the key. Unset, the one the provider reads, such as `ANTHROPIC_API_KEY`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L774)

<a id="E2E_AgentConfig_BaseUrl_"></a>
<a id="E2E_AgentConfig_BaseUrl"></a>

### BaseUrl


```csharp
public string? BaseUrl { get; init; }
```


The API root. Unset, the provider's own; for `azure` it is built from `AZURE_RESOURCE_NAME`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L771)

<a id="E2E_AgentConfig_Context_"></a>
<a id="E2E_AgentConfig_Context"></a>

### Context


```csharp
public string? Context { get; init; }
```


Project context told to every model call, at most 16384 UTF-8 bytes.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L768)

<a id="E2E_AgentConfig_Judge_"></a>
<a id="E2E_AgentConfig_Judge"></a>

### Judge


```csharp
public string? Judge { get; init; }
```


The model id that judges `assert`, `waitFor`, and `extract`. Unset, the judge is [Model](#E2E_AgentConfig_Model).

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L759)

<a id="E2E_AgentConfig_JudgmentTimeout_"></a>
<a id="E2E_AgentConfig_JudgmentTimeout"></a>

### JudgmentTimeout


```csharp
public TimeSpan JudgmentTimeout { get; init; }
```


**Value:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L782)

<a id="E2E_AgentConfig_MaxModelCalls_"></a>
<a id="E2E_AgentConfig_MaxModelCalls"></a>

### MaxModelCalls


```csharp
public int? MaxModelCalls { get; init; }
```


1 through 100. Null when the entry leaves it out: [MaxModelCalls](/api/E2E/E2EDefaults/#E2E_E2EDefaults_MaxModelCalls) applies, and `Explore` sets its own.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L780)

<a id="E2E_AgentConfig_MaxSteps_"></a>
<a id="E2E_AgentConfig_MaxSteps"></a>

### MaxSteps


```csharp
public int? MaxSteps { get; init; }
```


1 through 100. Null when the entry leaves it out: [MaxSteps](/api/E2E/E2EDefaults/#E2E_E2EDefaults_MaxSteps) applies, and `Explore` sets its own.

**Value:** [int?](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L777)

<a id="E2E_AgentConfig_Model_"></a>
<a id="E2E_AgentConfig_Model"></a>

### Model


```csharp
public string? Model { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L756)

<a id="E2E_AgentConfig_Provider_"></a>
<a id="E2E_AgentConfig_Provider"></a>

### Provider


```csharp
public string Provider { get; init; }
```


Who serves [Model](#E2E_AgentConfig_Model) and [Judge](#E2E_AgentConfig_Judge). One of [Providers](#E2E_AgentConfig_Providers); `openai` by default.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L762)

<a id="E2E_AgentConfig_ProviderOptions_"></a>
<a id="E2E_AgentConfig_ProviderOptions"></a>

### ProviderOptions


```csharp
public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
```


Provider options by provider. Each client adds the entry under its own key to its request body.

**Value:** [IReadOnlyDictionary&lt;string, JsonElement&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L785)

<a id="E2E_AgentConfig_Providers_"></a>
<a id="E2E_AgentConfig_Providers"></a>

### Providers


```csharp
public static IReadOnlyList<string> Providers { get; }
```


The `provider` values: API keys (`openai`, `openai-responses`, `azure`, `anthropic`,
`google`, `bedrock`, `xai`, `openrouter`, `gateway`, `openai-compatible`) and
subscription logins (`chatgpt`, `copilot`, `grok`, `opencode-console`).

**Value:** [IReadOnlyList&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L750)

<a id="E2E_AgentConfig_System_"></a>
<a id="E2E_AgentConfig_System"></a>

### System


```csharp
public string? System { get; init; }
```


Text appended to the act rules. Judges never see it.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L765)

## Methods

<a id="E2E_AgentConfig_CreateJudge_"></a>
<a id="E2E_AgentConfig_CreateJudge"></a>

### CreateJudge()


```csharp
public IAgentModel? CreateJudge()
```


The model for [Judge](#E2E_AgentConfig_Judge), or null when no judge is set.

**Returns:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L791)

<a id="E2E_AgentConfig_CreateModel_"></a>
<a id="E2E_AgentConfig_CreateModel"></a>

### CreateModel()


```csharp
public IAgentModel? CreateModel()
```


The model for [Model](#E2E_AgentConfig_Model), or null when no model is set.

**Returns:** [IAgentModel](/api/E2E/IAgentModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L788)

<a id="E2E_AgentConfig_CreateOptions_"></a>
<a id="E2E_AgentConfig_CreateOptions"></a>

### CreateOptions()


```csharp
public AgentOptions CreateOptions()
```


The [AgentOptions](/api/E2E/AgentOptions/) this entry describes, with its models created.

**Returns:** [AgentOptions](/api/E2E/AgentOptions/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/E2EConfig.cs#L794)
