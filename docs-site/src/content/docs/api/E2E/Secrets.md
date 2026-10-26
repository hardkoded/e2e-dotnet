---
title: "Secrets"
description: "The secrets declared in e2e.config.json. Each value comes from E2ESECRET{NAME} when it is set, else from the config. The name is uppercased and every charact..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Secrets"
  hidden: true
---

<a id="E2E_Secrets"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class Secrets
```


The secrets declared in `e2e.config.json`. Each value comes from
`E2E_SECRET_{NAME}` when it is set, else from the config. The name is
uppercased and every character other than A-Z and 0-9 becomes an underscore.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L148)

## Properties

<a id="E2E_Secrets_Empty_"></a>
<a id="E2E_Secrets_Empty"></a>

### Empty


```csharp
public static Secrets Empty { get; }
```


**Value:** [Secrets](./)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L157)

<a id="E2E_Secrets_Names_"></a>
<a id="E2E_Secrets_Names"></a>

### Names


```csharp
public IEnumerable<string> Names { get; }
```


**Value:** [IEnumerable&lt;string&gt;](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L159)

## Methods

<a id="E2E_Secrets_EnvironmentVariable_"></a>
<a id="E2E_Secrets_EnvironmentVariable_System_String_"></a>

### EnvironmentVariable(string)


```csharp
public static string EnvironmentVariable(string name)
```


The override variable for a secret name, such as `E2E_SECRET_STRIPE_KEY` for `stripe-key`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L162)

<a id="E2E_Secrets_Get_"></a>
<a id="E2E_Secrets_Get_System_String_"></a>

### Get(string)


```csharp
public Secret Get(string name)
```


The configured secret. A name the config does not declare throws `SECRET_UNAVAILABLE`.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [Secret](/api/E2E/Secret/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L175)
