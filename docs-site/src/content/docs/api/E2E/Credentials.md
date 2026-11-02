---
title: "Credentials"
description: "The accounts of the config's credentials (upstream's credentials, secrets.ts): E2E_USER_{NAME}_USERNAME and E2E_USER_{NAME}_PASSWORD replace the config's val..."
editUrl: false
prev: false
next: false
sidebar:
  label: "Credentials"
  hidden: true
---

<a id="E2E_Credentials"></a>

Namespace: [E2E](/api/E2E/)


```csharp
public static class Credentials
```


The accounts of the config's `credentials` (upstream's `credentials`, `secrets.ts`): `E2E_USER_{NAME}_USERNAME` and
`E2E_USER_{NAME}_PASSWORD` replace the config's values. While a session runs, only a configured name resolves.

**Inheritance:** [object](https://learn.microsoft.com/dotnet/api/system.object)

[View source](https://github.com/hardkoded/e2e-dotnet/blob/main/src/E2E/Values/Secret.cs)

## Methods

<a id="E2E_Credentials_User_"></a>
<a id="E2E_Credentials_User_System_String_"></a>

### User(string)


```csharp
public static UserCredential User(string name)
```


The named account. A name the session's config declares in `credentials` resolves with its password as the secret
`<name>.password`. .NET-only: a name the config does not declare, or a call outside a session, reads the
`E2E_USER_*` variables alone, as the port did before it read the config.

**Parameters**

| Name | Type | Description |
| --- | --- | --- |
| `name` | [string](https://learn.microsoft.com/dotnet/api/system.string) |  |

**Returns:** [UserCredential](/api/E2E/UserCredential/)
