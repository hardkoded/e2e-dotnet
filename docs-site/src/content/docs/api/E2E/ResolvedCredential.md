---
title: "ResolvedCredential"
description: "One config.credentials entry, resolved (upstream's ResolvedCredential): an account whose password is the secret {name}.password."
editUrl: false
prev: false
next: false
sidebar:
  label: "ResolvedCredential"
  hidden: true
---

<a id="E2E_ResolvedCredential"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed record ResolvedCredential : IEquatable<ResolvedCredential>
```


One `config.credentials` entry, resolved (upstream's `ResolvedCredential`): an account whose password is the secret `{name}.password`.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

**Implements:** [IEquatable&lt;ResolvedCredential&gt;](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Config/ConfigSecrets.cs)

## Constructors

<a id="E2E_ResolvedCredential__ctor_"></a>
<a id="E2E_ResolvedCredential__ctor_System_String_System_String_E2E_Secret_"></a>

### ResolvedCredential(string, string, Secret)


```csharp
public ResolvedCredential(string Name, string Username, Secret Password)
```


One `config.credentials` entry, resolved (upstream's `ResolvedCredential`): an account whose password is the secret `{name}.password`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `Name` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The entry's name in `credentials`. |
| `Username` | [string](https://learn.microsoft.com/dotnet/api/system.string) | The username from `E2E_USER_<NAME>_USERNAME` or the config. |
| `Password` | [Secret](/api/E2E/Secret/) | The password from `E2E_USER_<NAME>_PASSWORD` or the config, named `<name>.password`. |

## Properties

<a id="E2E_ResolvedCredential_Name_"></a>
<a id="E2E_ResolvedCredential_Name"></a>

### Name


```csharp
public string Name { get; init; }
```


The entry's name in `credentials`.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

<a id="E2E_ResolvedCredential_Password_"></a>
<a id="E2E_ResolvedCredential_Password"></a>

### Password


```csharp
public Secret Password { get; init; }
```


The password from `E2E_USER_<NAME>_PASSWORD` or the config, named `<name>.password`.

**Value:** [Secret](/api/E2E/Secret/)

<a id="E2E_ResolvedCredential_Username_"></a>
<a id="E2E_ResolvedCredential_Username"></a>

### Username


```csharp
public string Username { get; init; }
```


The username from `E2E_USER_<NAME>_USERNAME` or the config.

**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)
