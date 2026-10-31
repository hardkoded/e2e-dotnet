---
title: "AwsCredentials"
description: "AWS access key credentials for Signature Version 4."
editUrl: false
prev: false
next: false
sidebar:
  label: "AwsCredentials"
  hidden: true
---

<a id="E2E_AwsCredentials"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record AwsCredentials : IEquatable<AwsCredentials>
```


AWS access key credentials for Signature Version 4.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;AwsCredentials&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L367)

## Constructors

<a id="E2E_AwsCredentials__ctor_"></a>
<a id="E2E_AwsCredentials__ctor_System_String_System_String_System_String_"></a>

### AwsCredentials(string, string, string?)


```csharp
public AwsCredentials(string AccessKeyId, string SecretAccessKey, string? SessionToken = null)
```


AWS access key credentials for Signature Version 4.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `AccessKeyId` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `SecretAccessKey` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `SessionToken` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L367)

## Properties

<a id="E2E_AwsCredentials_AccessKeyId_"></a>
<a id="E2E_AwsCredentials_AccessKeyId"></a>

### AccessKeyId


```csharp
public string AccessKeyId { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L367)

<a id="E2E_AwsCredentials_SecretAccessKey_"></a>
<a id="E2E_AwsCredentials_SecretAccessKey"></a>

### SecretAccessKey


```csharp
public string SecretAccessKey { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L367)

<a id="E2E_AwsCredentials_SessionToken_"></a>
<a id="E2E_AwsCredentials_SessionToken"></a>

### SessionToken


```csharp
public string? SessionToken { get; init; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Agent/Models/BedrockModel.cs#L367)
