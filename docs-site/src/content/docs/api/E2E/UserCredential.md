---
title: "UserCredential"
description: "Username plus a Secret password loaded from the environment."
editUrl: false
prev: false
next: false
sidebar:
  label: "UserCredential"
  hidden: true
---

<a id="E2E_UserCredential"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public sealed class UserCredential
```


Username plus a [Secret](/api/E2E/Secret/) password loaded from the environment.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L100)

## Constructors

<a id="E2E_UserCredential__ctor_"></a>
<a id="E2E_UserCredential__ctor_System_String_E2E_Secret_"></a>

### UserCredential(string, Secret)


```csharp
public UserCredential(string username, Secret password)
```


**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `username` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |
| `password` | [Secret](/api/E2E/Secret/) |  |

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L102)

## Properties

<a id="E2E_UserCredential_Password_"></a>
<a id="E2E_UserCredential_Password"></a>

### Password


```csharp
public Secret Password { get; }
```


**Value:** [Secret](/api/E2E/Secret/)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L112)

<a id="E2E_UserCredential_Username_"></a>
<a id="E2E_UserCredential_Username"></a>

### Username


```csharp
public string Username { get; }
```


**Value:** [string](https://learn.microsoft.com/dotnet/api/system.string)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs#L110)
