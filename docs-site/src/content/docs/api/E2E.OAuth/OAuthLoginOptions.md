---
title: "OAuthLoginOptions"
description: "The e2e login flags; each provider reads the ones meant for it."
editUrl: false
prev: false
next: false
sidebar:
  label: "OAuthLoginOptions"
  hidden: true
---

<a id="E2E_OAuth_OAuthLoginOptions"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public sealed class OAuthLoginOptions
```


The `e2e login` flags; each provider reads the ones meant for it.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L54)

## Properties

<a id="E2E_OAuth_OAuthLoginOptions_ClientId_"></a>
<a id="E2E_OAuth_OAuthLoginOptions_ClientId"></a>

### ClientId


```csharp
public string? ClientId { get; init; }
```


GitHub Copilot: the client id of your GitHub OAuth App with the device flow enabled.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L60)

<a id="E2E_OAuth_OAuthLoginOptions_Device_"></a>
<a id="E2E_OAuth_OAuthLoginOptions_Device"></a>

### Device


```csharp
public bool Device { get; init; }
```


ChatGPT: print a code for another device instead of opening the browser.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L57)

<a id="E2E_OAuth_OAuthLoginOptions_EnterpriseUrl_"></a>
<a id="E2E_OAuth_OAuthLoginOptions_EnterpriseUrl"></a>

### EnterpriseUrl


```csharp
public string? EnterpriseUrl { get; init; }
```


GitHub Copilot: `github.example.com` for GitHub Enterprise.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L66)

<a id="E2E_OAuth_OAuthLoginOptions_FromGitHubCli_"></a>
<a id="E2E_OAuth_OAuthLoginOptions_FromGitHubCli"></a>

### FromGitHubCli


```csharp
public bool FromGitHubCli { get; init; }
```


GitHub Copilot: reuse `gh auth token` even when a client id is given.

**Value:** [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/OAuthProvider.cs#L63)
