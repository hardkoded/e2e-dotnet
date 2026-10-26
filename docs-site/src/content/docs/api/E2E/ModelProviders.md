---
title: "ModelProviders"
description: "Ready-made models for the providers upstream's e2e init and docs name, each reading the key from the environment variable the AI SDK reads. Subscription logi..."
editUrl: false
prev: false
next: false
sidebar:
  label: "ModelProviders"
  hidden: true
---

<a id="E2E_ModelProviders"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class ModelProviders
```


Ready-made models for the providers upstream's `e2e init` and docs name, each reading the key from the
environment variable the AI SDK reads. Subscription logins are in [Subscriptions](/api/E2E.OAuth/Subscriptions/).

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L13)

## Methods

<a id="E2E_ModelProviders_Anthropic_"></a>
<a id="E2E_ModelProviders_Anthropic_System_String_System_String_"></a>

### Anthropic(string, string?)


```csharp
public static AnthropicModel Anthropic(string model, string? apiKey = null)
```


Anthropic's Messages API, reading `ANTHROPIC_API_KEY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `apiKey` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [AnthropicModel](/api/E2E/AnthropicModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L54)

<a id="E2E_ModelProviders_AzureOpenAi_"></a>
<a id="E2E_ModelProviders_AzureOpenAi_System_String_System_String_System_String_"></a>

### AzureOpenAi(string, string?, string?)


```csharp
public static OpenAiResponsesModel AzureOpenAi(string deployment, string? resourceName = null, string? apiKey = null)
```


An Azure OpenAI deployment over the Responses API (`/openai/v1`), reading `AZURE_API_KEY`.
&lt;code class="paramref"&gt;resourceName&lt;/code&gt; defaults to `AZURE_RESOURCE_NAME`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `deployment` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `resourceName` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `apiKey` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [OpenAiResponsesModel](/api/E2E/OpenAiResponsesModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L31)

<a id="E2E_ModelProviders_AzureOpenAiAt_"></a>
<a id="E2E_ModelProviders_AzureOpenAiAt_System_String_System_String_System_String_"></a>

### AzureOpenAiAt(string, string, string?)


```csharp
public static OpenAiResponsesModel AzureOpenAiAt(string deployment, string baseUrl, string? apiKey = null)
```


An Azure OpenAI deployment at an explicit `/openai/v1` base URL, reading `AZURE_API_KEY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `deployment` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `baseUrl` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `apiKey` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [OpenAiResponsesModel](/api/E2E/OpenAiResponsesModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L39)

<a id="E2E_ModelProviders_Bedrock_"></a>
<a id="E2E_ModelProviders_Bedrock_System_String_System_String_"></a>

### Bedrock(string, string?)


```csharp
public static BedrockModel Bedrock(string model, string? region = null)
```


Amazon Bedrock's Converse API, signed from the AWS environment or with `AWS_BEARER_TOKEN_BEDROCK`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `region` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [BedrockModel](/api/E2E/BedrockModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L66)

<a id="E2E_ModelProviders_Gateway_"></a>
<a id="E2E_ModelProviders_Gateway_System_String_System_String_"></a>

### Gateway(string, string?)


```csharp
public static OpenAiCompatibleModel Gateway(string model, string? apiKey = null)
```


The Vercel AI Gateway over its OpenAI-compatible API, reading `AI_GATEWAY_API_KEY`, or without it the
`VERCEL_OIDC_TOKEN` that `vercel env pull` writes for a linked project.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `apiKey` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L101)

<a id="E2E_ModelProviders_Google_"></a>
<a id="E2E_ModelProviders_Google_System_String_System_String_"></a>

### Google(string, string?)


```csharp
public static GoogleModel Google(string model, string? apiKey = null)
```


Google's Gemini API, reading `GOOGLE_GENERATIVE_AI_API_KEY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `apiKey` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [GoogleModel](/api/E2E/GoogleModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L60)

<a id="E2E_ModelProviders_Ollama_"></a>
<a id="E2E_ModelProviders_Ollama_System_String_System_String_"></a>

### Ollama(string, string)


```csharp
public static OpenAiCompatibleModel Ollama(string model, string baseUrl = "http://127.0.0.1:11434/v1")
```


A local Ollama server's OpenAI-compatible API, no key.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `baseUrl` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L115)

<a id="E2E_ModelProviders_OpenAi_"></a>
<a id="E2E_ModelProviders_OpenAi_System_String_System_String_"></a>

### OpenAi(string, string?)


```csharp
public static OpenAiCompatibleModel OpenAi(string model, string? apiKey = null)
```


OpenAI over chat completions, reading `OPENAI_API_KEY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `apiKey` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L16)

<a id="E2E_ModelProviders_OpenAiResponses_"></a>
<a id="E2E_ModelProviders_OpenAiResponses_System_String_System_String_"></a>

### OpenAiResponses(string, string?)


```csharp
public static OpenAiResponsesModel OpenAiResponses(string model, string? apiKey = null)
```


OpenAI over the Responses API, reading `OPENAI_API_KEY`. The AI SDK's `openai(id)` calls this API.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `apiKey` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [OpenAiResponsesModel](/api/E2E/OpenAiResponsesModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L22)

<a id="E2E_ModelProviders_OpenRouter_"></a>
<a id="E2E_ModelProviders_OpenRouter_System_String_System_String_"></a>

### OpenRouter(string, string?)


```csharp
public static OpenAiCompatibleModel OpenRouter(string model, string? apiKey = null)
```


OpenRouter, reading `OPENROUTER_API_KEY`. Model ids name the upstream, such as `openai/gpt-6-luna-fast`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `apiKey` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L85)

<a id="E2E_ModelProviders_Xai_"></a>
<a id="E2E_ModelProviders_Xai_System_String_System_String_"></a>

### Xai(string, string?)


```csharp
public static OpenAiCompatibleModel Xai(string model, string? apiKey = null)
```


The SpaceXAI API, reading `XAI_API_KEY`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `model` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `apiKey` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [OpenAiCompatibleModel](/api/E2E/OpenAiCompatibleModel/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/ModelProviders.cs#L72)
