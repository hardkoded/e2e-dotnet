---
title: "BedrockModelOptions"
description: "BedrockModelOptions in E2E."
editUrl: false
prev: false
next: false
sidebar:
  label: "BedrockModelOptions"
  hidden: true
---

<a id="E2E_BedrockModelOptions"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class BedrockModelOptions
```


**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L254)

## Properties

<a id="E2E_BedrockModelOptions_ApiKey_"></a>
<a id="E2E_BedrockModelOptions_ApiKey"></a>

### ApiKey


```csharp
public string? ApiKey { get; init; }
```


A Bedrock API key, sent as a bearer token instead of signing.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L269)

<a id="E2E_BedrockModelOptions_ApiKeyEnv_"></a>
<a id="E2E_BedrockModelOptions_ApiKeyEnv"></a>

### ApiKeyEnv


```csharp
public string ApiKeyEnv { get; init; }
```


Environment variable read when [ApiKey](#E2E_BedrockModelOptions_ApiKey) is empty. Defaults to `AWS_BEARER_TOKEN_BEDROCK`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L272)

<a id="E2E_BedrockModelOptions_BaseUrl_"></a>
<a id="E2E_BedrockModelOptions_BaseUrl"></a>

### BaseUrl


```csharp
public string? BaseUrl { get; init; }
```


The runtime endpoint. Unset, it is `https://bedrock-runtime.{region}.amazonaws.com`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L263)

<a id="E2E_BedrockModelOptions_Credentials_"></a>
<a id="E2E_BedrockModelOptions_Credentials"></a>

### Credentials


```csharp
public AwsCredentials? Credentials { get; init; }
```


Signing credentials. Unset, they come from `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN` and, with those unset too, from the AWS credential chain (`AWS_PROFILE` or the default profile, SSO, container and instance roles).

**Value:** [AwsCredentials](/api/E2E/AwsCredentials/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L275)

<a id="E2E_BedrockModelOptions_MaxTokens_"></a>
<a id="E2E_BedrockModelOptions_MaxTokens"></a>

### MaxTokens


```csharp
public int MaxTokens { get; init; }
```


**Value:** [int](https://learn.microsoft.com/dotnet/api/system.int32)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L277)

<a id="E2E_BedrockModelOptions_Model_"></a>
<a id="E2E_BedrockModelOptions_Model"></a>

### Model


```csharp
public required string Model { get; init; }
```


A Bedrock model id or inference profile, such as `us.anthropic.claude-sonnet-4-5-20250929-v1:0`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L257)

<a id="E2E_BedrockModelOptions_Provider_"></a>
<a id="E2E_BedrockModelOptions_Provider"></a>

### Provider


```csharp
public string Provider { get; init; }
```


The key this client reads in [ProviderOptions](/api/E2E/ModelRequest/#E2E_ModelRequest_ProviderOptions). Its fields are added to the body as given.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L266)

<a id="E2E_BedrockModelOptions_Region_"></a>
<a id="E2E_BedrockModelOptions_Region"></a>

### Region


```csharp
public string? Region { get; init; }
```


The AWS region. Unset, it is `AWS_REGION`, then `AWS_DEFAULT_REGION`, then `us-east-1`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L260)

## Methods

<a id="E2E_BedrockModelOptions_ResolveApiKey_"></a>
<a id="E2E_BedrockModelOptions_ResolveApiKey"></a>

### ResolveApiKey()


```csharp
public string? ResolveApiKey()
```


**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L279)

<a id="E2E_BedrockModelOptions_ResolveCredentials_"></a>
<a id="E2E_BedrockModelOptions_ResolveCredentials"></a>

### ResolveCredentials()


```csharp
public AwsCredentials? ResolveCredentials()
```


**Returns:** [AwsCredentials](/api/E2E/AwsCredentials/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L289)

<a id="E2E_BedrockModelOptions_ResolveRegion_"></a>
<a id="E2E_BedrockModelOptions_ResolveRegion"></a>

### ResolveRegion()


```csharp
public string ResolveRegion()
```


**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L284)
