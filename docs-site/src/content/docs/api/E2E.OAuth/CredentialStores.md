---
title: "CredentialStores"
description: "The stores the subscription models and the CLI use."
editUrl: false
prev: false
next: false
sidebar:
  label: "CredentialStores"
  hidden: true
---

<a id="E2E_OAuth_CredentialStores"></a>

Namespace: [E2E.OAuth](/api/E2E.OAuth/)


```csharp
public static class CredentialStores
```


The stores the subscription models and the CLI use.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs#L25)

## Fields

<a id="E2E_OAuth_CredentialStores_EnvironmentVariable"></a>

### EnvironmentVariable


```csharp
public const string EnvironmentVariable = "E2E_OAUTH_CREDENTIALS"
```


Credentials JSON in the environment, standing in for the file on a machine the user cannot sign in on.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs#L28)

## Methods

<a id="E2E_OAuth_CredentialStores_Default_"></a>
<a id="E2E_OAuth_CredentialStores_Default"></a>

### Default()


```csharp
public static ICredentialStore Default()
```


[EnvironmentVariable](#E2E_OAuth_CredentialStores_EnvironmentVariable) when set, else the file at [DefaultPath()](#E2E_OAuth_CredentialStores_DefaultPath).

**Returns:** [ICredentialStore](/api/E2E.OAuth/ICredentialStore/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs#L44)

<a id="E2E_OAuth_CredentialStores_DefaultPath_"></a>
<a id="E2E_OAuth_CredentialStores_DefaultPath"></a>

### DefaultPath()


```csharp
public static string DefaultPath()
```


`$XDG_CONFIG_HOME/e2e/oauth.json`, or `~/.config/e2e/oauth.json`: the file upstream's
`e2e login` writes, so a login made with either CLI serves both.

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/OAuth/CredentialStore.cs#L34)
